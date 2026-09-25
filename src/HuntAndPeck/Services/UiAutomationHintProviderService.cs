using HuntAndPeck.Extensions;
using HuntAndPeck.Models;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Interop.UIAutomationClient;

namespace HuntAndPeck.Services
{
    internal class UiAutomationHintProviderService : IHintProviderService, IDebugHintProviderService
    {
        private readonly IUIAutomation _automation = new CUIAutomation();

        /// <summary>
        /// Matches enabled, on screen elements in the control view
        /// </summary>
        private readonly IUIAutomationCondition _enabledOnScreenCondition;

        /// <summary>
        /// As <see cref="_enabledOnScreenCondition"/>, but also requires at least one pattern a hint can be created from.
        /// Filtering on the provider side avoids marshalling elements that would be discarded anyway.
        /// </summary>
        private readonly IUIAutomationCondition _hintableCondition;

        /// <summary>
        /// Fetches everything <see cref="CreateHint"/> needs in the same cross-process call as the search,
        /// rather than several round trips per element
        /// </summary>
        private readonly IUIAutomationCacheRequest _cacheRequest;

        public UiAutomationHintProviderService()
        {
            var conditionControlView = _automation.ControlViewCondition;
            var conditionEnabled = _automation.CreatePropertyCondition(UIA_PropertyIds.UIA_IsEnabledPropertyId, true);
            var enabledControlCondition = _automation.CreateAndCondition(conditionControlView, conditionEnabled);

            var conditionOnScreen = _automation.CreatePropertyCondition(UIA_PropertyIds.UIA_IsOffscreenPropertyId, false);
            _enabledOnScreenCondition = _automation.CreateAndCondition(enabledControlCondition, conditionOnScreen);

            var patternAvailableConditions = new[]
            {
                UIA_PropertyIds.UIA_IsInvokePatternAvailablePropertyId,
                UIA_PropertyIds.UIA_IsTogglePatternAvailablePropertyId,
                UIA_PropertyIds.UIA_IsSelectionItemPatternAvailablePropertyId,
                UIA_PropertyIds.UIA_IsExpandCollapsePatternAvailablePropertyId,
                UIA_PropertyIds.UIA_IsValuePatternAvailablePropertyId,
                UIA_PropertyIds.UIA_IsRangeValuePatternAvailablePropertyId,
            }.Select(id => _automation.CreatePropertyCondition(id, true)).ToArray();
            var anyPatternAvailable = _automation.CreateOrConditionFromArray(patternAvailableConditions);
            _hintableCondition = _automation.CreateAndCondition(_enabledOnScreenCondition, anyPatternAvailable);

            _cacheRequest = _automation.CreateCacheRequest();
            _cacheRequest.AddProperty(UIA_PropertyIds.UIA_BoundingRectanglePropertyId);
            _cacheRequest.AddProperty(UIA_PropertyIds.UIA_IsInvokePatternAvailablePropertyId);
            _cacheRequest.AddProperty(UIA_PropertyIds.UIA_IsTogglePatternAvailablePropertyId);
            _cacheRequest.AddProperty(UIA_PropertyIds.UIA_IsSelectionItemPatternAvailablePropertyId);
            _cacheRequest.AddProperty(UIA_PropertyIds.UIA_IsExpandCollapsePatternAvailablePropertyId);
            _cacheRequest.AddProperty(UIA_PropertyIds.UIA_IsValuePatternAvailablePropertyId);
            _cacheRequest.AddProperty(UIA_PropertyIds.UIA_ValueIsReadOnlyPropertyId);
            _cacheRequest.AddProperty(UIA_PropertyIds.UIA_IsRangeValuePatternAvailablePropertyId);
            _cacheRequest.AddProperty(UIA_PropertyIds.UIA_RangeValueIsReadOnlyPropertyId);
            _cacheRequest.AddPattern(UIA_PatternIds.UIA_InvokePatternId);
            _cacheRequest.AddPattern(UIA_PatternIds.UIA_TogglePatternId);
            _cacheRequest.AddPattern(UIA_PatternIds.UIA_SelectionItemPatternId);
            _cacheRequest.AddPattern(UIA_PatternIds.UIA_ExpandCollapsePatternId);
        }

        public HintSession EnumHints()
        {
            var foregroundWindow = User32.GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                return null;
            }
            return EnumHints(foregroundWindow);
        }

        public HintSession EnumHints(IntPtr hWnd)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var session = EnumWindowHints(hWnd, _hintableCondition, CreateHint);
            sw.Stop();

            Debug.WriteLine("Enumeration of hints took {0} ms", sw.ElapsedMilliseconds);
            return session;
        }

        public HintSession EnumDebugHints()
        {
            var foregroundWindow = User32.GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                return null;
            }
            return EnumDebugHints(foregroundWindow);
        }

        public HintSession EnumDebugHints(IntPtr hWnd)
        {
            return EnumWindowHints(hWnd, _enabledOnScreenCondition, CreateDebugHint);
        }

        /// <summary>
        /// Enumerates all the hints from the given window
        /// </summary>
        /// <param name="hWnd">The window to get hints from</param>
        /// <param name="condition">The condition elements must match</param>
        /// <param name="hintFactory">The factory to use to create each hint in the session</param>
        /// <returns>A hint session</returns>
        private HintSession EnumWindowHints(IntPtr hWnd, IUIAutomationCondition condition, Func<IntPtr, Rect, IUIAutomationElement, Hint> hintFactory)
        {
            var result = new List<Hint>();
            var elements = EnumElements(hWnd, condition);

            // Window bounds
            var rawWindowBounds = new RECT();
            User32.GetWindowRect(hWnd, ref rawWindowBounds);
            Rect windowBounds = rawWindowBounds;

            foreach (var element in elements)
            {
                var boundingRectObject = element.CachedBoundingRectangle;
                if ((boundingRectObject.right > boundingRectObject.left) && (boundingRectObject.bottom > boundingRectObject.top))
                {
                    var niceRect = new Rect(new Point(boundingRectObject.left, boundingRectObject.top), new Point(boundingRectObject.right, boundingRectObject.bottom));
                    // Convert the bounding rect to logical coords
                    var logicalRect = niceRect.PhysicalToLogicalRect(hWnd);
                    if (!logicalRect.IsEmpty)
                    {
                        var windowCoords = niceRect.ScreenToWindowCoordinates(windowBounds);
                        var hint = hintFactory(hWnd, windowCoords, element);
                        if (hint != null)
                        {
                            result.Add(hint);
                        }
                    }
                }
            }

            return new HintSession
            {
                Hints = result,
                OwningWindow = hWnd,
                OwningWindowBounds = windowBounds,
            };
        }

        /// <summary>
        /// Enumerates the automation elements from the given window
        /// </summary>
        /// <param name="hWnd">The window handle</param>
        /// <param name="condition">The condition elements must match</param>
        /// <returns>All of the automation elements found, populated with <see cref="_cacheRequest"/></returns>
        private List<IUIAutomationElement> EnumElements(IntPtr hWnd, IUIAutomationCondition condition)
        {
            var result = new List<IUIAutomationElement>();
            var automationElement = _automation.ElementFromHandle(hWnd);

            var elementArray = automationElement.FindAllBuildCache(TreeScope.TreeScope_Descendants, condition, _cacheRequest);
            if (elementArray != null)
            {
                var length = elementArray.Length;
                result.Capacity = length;
                for (var i = 0; i < length; ++i)
                {
                    result.Add(elementArray.GetElement(i));
                }
            }

            return result;
        }

        /// <summary>
        /// Creates a UI Automation element from the given automation element
        /// </summary>
        /// <param name="owningWindow">The owning window</param>
        /// <param name="hintBounds">The hint bounds</param>
        /// <param name="automationElement">The associated automation element</param>
        /// <returns>The created hint, else null if the hint could not be created</returns>
        private Hint CreateHint(IntPtr owningWindow, Rect hintBounds, IUIAutomationElement automationElement)
        {
            try
            {
                if (IsCachedTrue(automationElement, UIA_PropertyIds.UIA_IsInvokePatternAvailablePropertyId))
                {
                    var invokePattern = (IUIAutomationInvokePattern)automationElement.GetCachedPattern(UIA_PatternIds.UIA_InvokePatternId);
                    return new UiAutomationInvokeHint(owningWindow, invokePattern, hintBounds);
                }

                if (IsCachedTrue(automationElement, UIA_PropertyIds.UIA_IsTogglePatternAvailablePropertyId))
                {
                    var togglePattern = (IUIAutomationTogglePattern)automationElement.GetCachedPattern(UIA_PatternIds.UIA_TogglePatternId);
                    return new UiAutomationToggleHint(owningWindow, togglePattern, hintBounds);
                }

                if (IsCachedTrue(automationElement, UIA_PropertyIds.UIA_IsSelectionItemPatternAvailablePropertyId))
                {
                    var selectPattern = (IUIAutomationSelectionItemPattern)automationElement.GetCachedPattern(UIA_PatternIds.UIA_SelectionItemPatternId);
                    return new UiAutomationSelectHint(owningWindow, selectPattern, hintBounds);
                }

                if (IsCachedTrue(automationElement, UIA_PropertyIds.UIA_IsExpandCollapsePatternAvailablePropertyId))
                {
                    var expandCollapsePattern = (IUIAutomationExpandCollapsePattern)automationElement.GetCachedPattern(UIA_PatternIds.UIA_ExpandCollapsePatternId);
                    return new UiAutomationExpandCollapseHint(owningWindow, expandCollapsePattern, hintBounds);
                }

                if (IsCachedTrue(automationElement, UIA_PropertyIds.UIA_IsValuePatternAvailablePropertyId) &&
                    !IsCachedTrue(automationElement, UIA_PropertyIds.UIA_ValueIsReadOnlyPropertyId))
                {
                    return new UiAutomationFocusHint(owningWindow, automationElement, hintBounds);
                }

                if (IsCachedTrue(automationElement, UIA_PropertyIds.UIA_IsRangeValuePatternAvailablePropertyId) &&
                    !IsCachedTrue(automationElement, UIA_PropertyIds.UIA_RangeValueIsReadOnlyPropertyId))
                {
                    return new UiAutomationFocusHint(owningWindow, automationElement, hintBounds);
                }

                return null;
            }
            catch (Exception)
            {
                // May have gone
                return null;
            }
        }

        private static bool IsCachedTrue(IUIAutomationElement automationElement, int propertyId)
        {
            return automationElement.GetCachedPropertyValue(propertyId) is true;
        }

        /// <summary>
        /// Creates a debug hint
        /// </summary>
        /// <param name="owningWindow">The window that owns the hint</param>
        /// <param name="hintBounds">The hint bounds</param>
        /// <param name="automationElement">The automation element</param>
        /// <returns>A debug hint</returns>
        private DebugHint CreateDebugHint(IntPtr owningWindow, Rect hintBounds, IUIAutomationElement automationElement)
        {
            // Enumerate all possible patterns. Note that the performance of this is *very* bad -- hence debug only.
            var programmaticNames = new List<string>();

            foreach (var pn in UiAutomationPatternIds.PatternNames)
            {
                try
                {
                    var pattern = automationElement.GetCurrentPattern(pn.Key);
                    if(pattern != null)
                    {
                        programmaticNames.Add(pn.Value);
                    }
                }
                catch (Exception)
                {
                }
            }

            if (programmaticNames.Any())
            {
                return new DebugHint(owningWindow, hintBounds, programmaticNames.ToList());
            }

            return null;
        }
    }
}
