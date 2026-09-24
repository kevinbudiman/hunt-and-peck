using HuntAndPeck.Extensions;
using HuntAndPeck.Models;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services;
using Interop.UIAutomationClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Xunit;
using Rect = System.Windows.Rect;
using Point = System.Windows.Point;
using WinForms = System.Windows.Forms;

namespace HuntAndPeck.Tests.Services
{
    public class UiAutomationHintProviderServiceTest
    {
        [Fact]
        public void EnumHints_MatchesUncachedEnumeration()
        {
            using var window = TestWindow.Create(form => AddMixedControls(form));
            var service = new UiAutomationHintProviderService();

            var expected = ReferenceEnumHints(window.Handle);
            var actual = Describe(service.EnumHints(window.Handle));

            Assert.NotEmpty(expected);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void EnumHints_CachedPatternsCanBeInvoked()
        {
            Button button = null;
            WinForms.CheckBox checkBox = null;
            WinForms.TextBox readOnlyTextBox = null;
            using var window = TestWindow.Create(form =>
            {
                (button, checkBox, readOnlyTextBox) = AddMixedControls(form);
            });
            var service = new UiAutomationHintProviderService();

            var hints = service.EnumHints(window.Handle).Hints;

            var buttonHint = Assert.Single(hints, h => Contains(h, window.Invoke(() => button.Bounds)));
            Assert.IsType<UiAutomationInvokeHint>(buttonHint);
            buttonHint.Invoke();
            Assert.True(button.Clicked.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken), "Invoke hint did not click the button");

            var checkBoxHint = Assert.Single(hints, h => Contains(h, window.Invoke(() => checkBox.Bounds)));
            Assert.IsType<UiAutomationToggleHint>(checkBoxHint);
            checkBoxHint.Invoke();
            Assert.True(window.Invoke(() => checkBox.Checked), "Toggle hint did not check the check box");

            Assert.DoesNotContain(hints, h => Contains(h, window.Invoke(() => readOnlyTextBox.Bounds)));
        }

        [Fact]
        public void EnumHints_ManyElements_Timing()
        {
            const int columns = 20;
            const int rows = 20;
            using var window = TestWindow.Create(form =>
            {
                for (var row = 0; row < rows; ++row)
                {
                    for (var column = 0; column < columns; ++column)
                    {
                        form.Controls.Add(new WinForms.Button
                        {
                            Text = $"{row},{column}",
                            Bounds = new System.Drawing.Rectangle(column * 38, row * 28, 36, 26),
                        });
                    }
                }
            });
            var service = new UiAutomationHintProviderService();

            // Warm up both paths so neither pays for proxy/provider initialisation
            ReferenceEnumHints(window.Handle);
            service.EnumHints(window.Handle);

            var sw = Stopwatch.StartNew();
            var expected = ReferenceEnumHints(window.Handle);
            var referenceMs = sw.ElapsedMilliseconds;

            sw.Restart();
            var actual = Describe(service.EnumHints(window.Handle));
            var cachedMs = sw.ElapsedMilliseconds;

            Assert.Equal(rows * columns, actual.Count);
            Assert.Equal(expected, actual);
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"{actual.Count} hints: uncached {referenceMs} ms, cached {cachedMs} ms");
        }

        private static (Button, WinForms.CheckBox, WinForms.TextBox) AddMixedControls(WinForms.Form form)
        {
            var button = new Button { Text = "Go", Bounds = new System.Drawing.Rectangle(10, 10, 100, 30) };
            var checkBox = new WinForms.CheckBox { Text = "Check", Bounds = new System.Drawing.Rectangle(10, 50, 100, 30) };
            var textBox = new WinForms.TextBox { Bounds = new System.Drawing.Rectangle(10, 90, 200, 30) };
            var readOnlyTextBox = new WinForms.TextBox { ReadOnly = true, Text = "Read only", Bounds = new System.Drawing.Rectangle(10, 130, 200, 30) };
            var label = new WinForms.Label { Text = "Label", Bounds = new System.Drawing.Rectangle(10, 170, 100, 30) };
            var comboBox = new WinForms.ComboBox { Bounds = new System.Drawing.Rectangle(10, 210, 200, 30) };
            comboBox.Items.AddRange(new object[] { "One", "Two" });
            var trackBar = new WinForms.TrackBar { Bounds = new System.Drawing.Rectangle(10, 250, 200, 45) };
            var disabledButton = new WinForms.Button { Text = "Disabled", Enabled = false, Bounds = new System.Drawing.Rectangle(10, 300, 100, 30) };
            form.Controls.AddRange(new WinForms.Control[] { button, checkBox, textBox, readOnlyTextBox, label, comboBox, trackBar, disabledButton });
            return (button, checkBox, readOnlyTextBox);
        }

        /// <summary>
        /// The original per-element, uncached enumeration, kept as the behavioural reference
        /// </summary>
        private static List<string> ReferenceEnumHints(IntPtr hWnd)
        {
            var automation = new CUIAutomation();
            var conditionEnabled = automation.CreatePropertyCondition(UIA_PropertyIds.UIA_IsEnabledPropertyId, true);
            var conditionOnScreen = automation.CreatePropertyCondition(UIA_PropertyIds.UIA_IsOffscreenPropertyId, false);
            var condition = automation.CreateAndCondition(
                automation.CreateAndCondition(automation.ControlViewCondition, conditionEnabled), conditionOnScreen);

            var rawWindowBounds = new RECT();
            User32.GetWindowRect(hWnd, ref rawWindowBounds);
            Rect windowBounds = rawWindowBounds;

            var result = new List<string>();
            var elements = automation.ElementFromHandle(hWnd).FindAll(TreeScope.TreeScope_Descendants, condition);
            for (var i = 0; i < elements.Length; ++i)
            {
                var element = elements.GetElement(i);
                var r = element.CurrentBoundingRectangle;
                if (r.right <= r.left || r.bottom <= r.top)
                {
                    continue;
                }

                var screenBounds = new Rect(new Point(r.left, r.top), new Point(r.right, r.bottom));
                if (screenBounds.PhysicalToLogicalRect(hWnd).IsEmpty)
                {
                    continue;
                }

                var type = ReferenceHintType(element);
                if (type != null)
                {
                    result.Add($"{type} {screenBounds.ScreenToWindowCoordinates(windowBounds)}");
                }
            }

            return result;
        }

        private static string ReferenceHintType(IUIAutomationElement element)
        {
            if (element.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) != null) return nameof(UiAutomationInvokeHint);
            if (element.GetCurrentPattern(UIA_PatternIds.UIA_TogglePatternId) != null) return nameof(UiAutomationToggleHint);
            if (element.GetCurrentPattern(UIA_PatternIds.UIA_SelectionItemPatternId) != null) return nameof(UiAutomationSelectHint);
            if (element.GetCurrentPattern(UIA_PatternIds.UIA_ExpandCollapsePatternId) != null) return nameof(UiAutomationExpandCollapseHint);
            if (element.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId) is IUIAutomationValuePattern value && value.CurrentIsReadOnly == 0) return nameof(UiAutomationFocusHint);
            if (element.GetCurrentPattern(UIA_PatternIds.UIA_RangeValuePatternId) is IUIAutomationRangeValuePattern range && range.CurrentIsReadOnly == 0) return nameof(UiAutomationFocusHint);
            return null;
        }

        private static List<string> Describe(HintSession session)
        {
            return session.Hints.Select(h => $"{h.GetType().Name} {h.BoundingRectangle}").ToList();
        }

        private static bool Contains(Hint hint, System.Drawing.Rectangle controlBounds)
        {
            // The test window is borderless, so window coordinates are client coordinates
            var centre = new Point(controlBounds.X + controlBounds.Width / 2.0, controlBounds.Y + controlBounds.Height / 2.0);
            return hint.BoundingRectangle.Contains(centre);
        }

        private sealed class Button : WinForms.Button
        {
            public ManualResetEventSlim Clicked { get; } = new ManualResetEventSlim();

            protected override void OnClick(EventArgs e)
            {
                base.OnClick(e);
                Clicked.Set();
            }
        }

        /// <summary>
        /// A borderless window running on its own UI thread, so UI Automation calls from the test thread are serviced
        /// </summary>
        private sealed class TestWindow : IDisposable
        {
            private readonly Thread _thread;
            private WinForms.Form _form;

            private TestWindow(Action<WinForms.Form> populate)
            {
                using var shown = new ManualResetEventSlim();
                _thread = new Thread(() =>
                {
                    _form = new WinForms.Form
                    {
                        FormBorderStyle = WinForms.FormBorderStyle.None,
                        StartPosition = WinForms.FormStartPosition.Manual,
                        Bounds = new System.Drawing.Rectangle(0, 0, 800, 600),
                        ShowInTaskbar = false,
                    };
                    populate(_form);
                    _form.Shown += (s, e) => shown.Set();
                    WinForms.Application.Run(_form);
                });
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.IsBackground = true;
                _thread.Start();
                Assert.True(shown.Wait(TimeSpan.FromSeconds(10)), "Test window was not shown");
                Handle = Invoke(() => _form.Handle);
            }

            public static TestWindow Create(Action<WinForms.Form> populate)
            {
                return new TestWindow(populate);
            }

            public IntPtr Handle { get; }

            public T Invoke<T>(Func<T> func)
            {
                return (T)_form.Invoke(func);
            }

            public void Dispose()
            {
                _form.BeginInvoke(new Action(_form.Close));
                _thread.Join(TimeSpan.FromSeconds(10));
            }
        }
    }
}
