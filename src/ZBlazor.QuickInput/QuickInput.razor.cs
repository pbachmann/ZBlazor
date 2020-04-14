﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ZBlazor.QuickInput;

namespace ZBlazor
{
    public partial class QuickInput<TItem> : ComponentBase
        where TItem : class
    {
        #region FIELDS

        int selectedItemIndex = -1;
        bool isOpen = false;
        bool hasInputValue => InputValue != "";
        bool isMouseDown = false;
        bool isFocused = false;

        readonly FuzzyMatcher _fuzzyMatcher = new FuzzyMatcher();

        List<SearchItem<TItem>> SearchItems = new List<SearchItem<TItem>>();

        SearchItem<TItem>? lastSelectedItem = null;

        #endregion FIELDS

        #region PARAMETERS

        /// <summary>
        /// The actual value of the input.
        /// </summary>
        [Parameter] public string? InputValue { get; set; } = "";

        [Parameter] public IEnumerable<TItem> Data { get; set; } = null!;

        /// <summary>
        /// When the type of TItem is not a string, this denotes the field name that should be used to display the item text. Defaults to "Text".
        /// </summary>
        [Parameter] public string? TextField { get; set; } = "Text";

        /// <summary>
        /// Any additional class(es) to apply to the actual input box.
        /// </summary>
        [Parameter] public string Class { get; set; } = "";

        /// <summary>
        /// Any additional class(es) to apply to the outer div containing the input.
        /// </summary>
        [Parameter] public string ContainerClass { get; set; } = "";

        /// <summary>
        /// The width of the component. Defaults to "300px".
        /// </summary>
        [Parameter] public string Width { get; set; } = "300px";

        /// <summary>
        /// When true, the list will open when the component gains focus. Defaults to true.
        /// </summary>
        [Parameter] public bool OpenOnFocus { get; set; } = true;

        /// <summary>
        /// When true, the first match will be selected by default. Defaults to true;
        /// </summary>
        [Parameter] public bool SelectFirstMatch { get; set; } = true;

        /// <summary>
        /// Denotes the maximum number of items to show at a given time. Defaults to 5. Set to 0 for no limit.
        /// </summary>
        [Parameter] public int MaxItemsToShow { get; set; } = 5;

        /// <summary>
        /// Maximum height for the items container.
        /// </summary>
        [Parameter] public string? MaxItemsHeight { get; set; }

        /// <summary>
        /// When true, highlights matches in the list. Defaults to true.
        /// </summary>
        [Parameter] public bool HighlightMatches { get; set; } = true;

        /// <summary>
        /// When true, clears the input value when the Escape key is pressed and the input has focus. Defaults to true.
        /// </summary>
        [Parameter] public bool ClearOnEscape { get; set; } = true;

        /// <summary>
        /// When true, a clear button will show when the input has a value. Defaults to true.
        /// </summary>
        [Parameter] public bool ShowClearButton { get; set; } = true;

        /// <summary>
        /// When true, the input in cleared when a value is selected. Defaults to false.
        /// </summary>
        [Parameter] public bool ClearAfterSelection { get; set; } = false;

        /// <summary>
        /// When present, will be placed next to (after) the input. For things like icon overlays.
        /// </summary>
        [Parameter] public RenderFragment? InputAccessory { get; set; }

        /// <summary>
        /// When populated, will display when no matches are found for an input value.
        /// </summary>
        [Parameter] public RenderFragment? NoResultsView { get; set; }

        /// <summary>
        /// When populated, the matcher will also check other named fields on <see cref="TItem" /> for a filter match.
        /// </summary>
        [Parameter] public IEnumerable<string>? OtherMatchFields { get; set; }

        /// <summary>
        /// When true, shows the name and value of the "other match" fields when matched. Defaults to true. Note that if you use an ItemTemplate you need to pass a value for GetDisplayText if you don't want other matches to show.
        /// </summary>
        [Parameter] public bool ShowOtherMatches { get; set; } = true;

        /// <summary>
        /// Occurs when the user selects a value from the list.
        /// </summary>
        [Parameter] public EventCallback<TItem?> OnItemSelected { get; set; }

        /// <summary>
        /// When true, results will be ordered with shorter values first. Takes precedence above <see cref="PrioritizePrimaryMatch"/>. Defaults to false.
        /// </summary>
        [Parameter] public bool PrioritizeShorterValues { get; set; } = false;

        /// <summary>
        /// When other (non-primary) fields match, sort primary matches to the top always. Defaults to false.
        /// </summary>
        [Parameter] public bool PrioritizePrimaryMatch { get; set; } = false;

        /// <summary>
        /// When true, input not matching any search item is allowed, otherwise input is cleared when not matching any search items. Defaults to true.
        /// </summary>
        [Parameter] public bool AllowCustomValues { get; set; } = true;

        /// <summary>
        /// The type of the input element. Defaults to 'text'.
        /// </summary>
        [Parameter] public string InputType { get; set; } = "text";

        /// <summary>
        /// Occurs when the user changes the value inside the input.
        /// </summary>
        [Parameter] public EventCallback<string> OnInputValueChanged { get; set; }

        /// <summary>
        /// When true, a null value will be passed to <see cref="OnItemSelected"/>. Defaults to true.
        /// </summary>
        [Parameter] public bool EmitNullOnInputClear { get; set; } = true;

        /// <summary>
        /// The repository to use for holding recent selected records. When populated, recents will be used, otherwise recent functionality will be disabled. Requires the <see cref="KeyField"/> to be populated with a valid unique key for <see cref="TItem"/>.
        /// </summary>
        [Parameter] public IRecentRepository? RecentRepository { get; set; }

        /// <summary>
        /// When present, is the field containing a unique key for the <see cref="TItem"/>.
        /// </summary>
        [Parameter] public string? KeyField { get; set; }

        [Parameter] public RenderFragment<SearchItem<TItem>>? ItemTemplate { get; set; }

        [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object> InputAttributes { get; set; } = new Dictionary<string, object>();

        #endregion PARAMETERS

        #region LIFECYCLE

        int previousCycleDataCount = 0;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            var currentCycleDataCount = Data?.Count() ?? 0;

            // TODO: this has a bug if the data were to be replaced with a different set with the same number of items
            if (previousCycleDataCount != currentCycleDataCount)
            {
                await InitializeSearchItems();

                previousCycleDataCount = currentCycleDataCount;
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        protected override void OnParametersSet()
        {
            selectedItemIndex = SelectFirstMatch ? 0 : -1;
            base.OnParametersSet();
        }

        #endregion LIFECYCLE

        #region EVENTS

        async Task OnValueChange(ChangeEventArgs args)
        {
            InputValue = (args.Value as string) ?? "";

            if (OnInputValueChanged.HasDelegate)
            {
                await OnInputValueChanged.InvokeAsync(InputValue);
            }

            isOpen = OpenOnFocus || hasInputValue;

            Calculate();
        }

        async Task OnSelected(SearchItem<TItem>? item)
        {
            lastSelectedItem = item;
            InputValue = item?.Text ?? "";

            if (RecentRepository != null && item != null)
            {
                await RecentRepository.AddHit(item.Key);
                item.LastHit = DateTime.Now;
            }

            if (OnItemSelected.HasDelegate)
            {
                await OnItemSelected.InvokeAsync(item?.DataObject);
            }

            if (ClearAfterSelection)
            {
                ClearInputValue();
            }
            else
            {
                Calculate();
            }

            isOpen = false;
        }

        void OnFocus(FocusEventArgs args)
        {
            if (OpenOnFocus)
            {
                isOpen = true;
            }

            isFocused = true;
        }

        async Task OnBlur(FocusEventArgs args)
        {
            await Task.Delay(100);
            if (!isMouseDown)
            {
                isOpen = false;

                Debug.WriteLine($"OnBlur InputValue: {InputValue}");

                if (!AllowCustomValues &&
                    (string.IsNullOrWhiteSpace(InputValue) || SearchItems.All(i => i.Text != InputValue)))
                {
                    if (string.IsNullOrWhiteSpace(InputValue))
                    {
                        if (lastSelectedItem != null)
                        {
                            await OnSelected(null);
                            Calculate();
                        }
                    }
                    else
                    {
                        InputValue = lastSelectedItem?.Text;
                    }
                }
            }

            isFocused = false;
        }

        async Task OnKeyDown(KeyboardEventArgs args)
        {
            switch (args.Code)
            {
                case "ArrowDown":
                    if (selectedItemIndex + 1 >= SearchItems.Where(i => i.ShouldItemShow).Count())
                    {
                        selectedItemIndex = 0;
                    }
                    else
                    {
                        selectedItemIndex += 1;
                    }
                    isOpen = true;
                    break;
                case "ArrowUp":
                    if (selectedItemIndex - 1 <= -1)
                    {
                        selectedItemIndex = SearchItems.Where(i => i.ShouldItemShow).Count() - 1;
                    }
                    else
                    {
                        selectedItemIndex -= 1;
                    }
                    isOpen = true;
                    break;
                case "Enter":
                    var selectedItem = SearchItems.SingleOrDefault(i => i.IsSelected);
                    if (selectedItem != null)
                    {
                        await OnSelected(selectedItem);
                    }

                    Calculate();
                    break;
                case "Escape":
                    if (!hasInputValue)
                    {
                        isOpen = !isOpen;
                    }
                    else if (ClearOnEscape)
                    {
                        ClearInputValue();
                    }
                    break;
            }
        }

        void OnMouseDown(MouseEventArgs args)
        {
            isMouseDown = true;

            if (isFocused && OpenOnFocus && !hasInputValue && !isOpen)
            {
                isOpen = true;
            }
        }

        void OnMouseUp(MouseEventArgs args)
        {
            isMouseDown = false;
        }

        #endregion EVENTS

        #region METHODS

        void Calculate()
        {
            selectedItemIndex = -1 + (SelectFirstMatch ? 1 : 0);

            foreach (var item in SearchItems)
            {
                ClearItem(item);

                var match = _fuzzyMatcher.Match(InputValue ?? "", item.Text);
                item.Matches = match.Matches;
                item.Score = match.Score;

                if (OtherMatchFields != null && OtherMatchFields.Count() > 0)
                {
                    var itemType = item?.DataObject?.GetType();

                    if (itemType == null)
                    {
                        break;
                    }

                    foreach (var otherField in OtherMatchFields)
                    {
                        Debug.WriteLine($"Searching OtherMatchFields field {otherField}");

                        var otherFieldValue = itemType.GetProperty(otherField)?.GetValue(item!.DataObject)?.ToString();

                        if (string.IsNullOrWhiteSpace(otherFieldValue))
                        {
                            continue;
                        }

                        var otherMatch = _fuzzyMatcher.Match(InputValue ?? "", otherFieldValue);

                        if (otherMatch != null && otherMatch.Score > item!.Score)
                        {
                            item.Score = otherMatch.Score;
                            item.Matches = otherMatch.Matches;
                            item.OtherMatchFieldName = otherField;
                            item.OtherMatchFieldValue = otherFieldValue;
                        }
                    }
                }
            }
        }

        protected virtual List<SearchItem<TItem>> GetOrderedSearchItems()
        {
            if (!hasInputValue)
            {
                if (RecentRepository != null)
                {
                    return SearchItems.OrderByDescending(i => i.LastHit).ToList();
                }

                return SearchItems;
            }

            IOrderedEnumerable<SearchItem<TItem>> ordered = null!;

            if (PrioritizeShorterValues)
            {
                ordered = SearchItems.OrderBy(i => i.Text.Length);
            }

            if (PrioritizePrimaryMatch)
            {
                if (PrioritizeShorterValues)
                {
                    ordered = ordered
                        .ThenByDescending(i => i.IsPrimaryMatch);
                }
                else
                {
                    ordered = SearchItems
                        .OrderByDescending(i => i.IsPrimaryMatch);
                }
            }

            if (PrioritizePrimaryMatch || PrioritizeShorterValues)
            {
                return ordered.ThenByDescending(i => i.Score).ToList();
            }
            else
            {
                return SearchItems.OrderByDescending(i => i.Score).ToList();
            }

        }

        private void ClearItem(SearchItem<TItem> item)
        {
            item.Matches = null;
            item.Score = -100;
            item.OtherMatchFieldName = null;
            item.OtherMatchFieldValue = null;
        }

        async Task InitializeSearchItems()
        {
            if (Data == null)
            {
                Debug.WriteLine("InitializeSearchItems: Data null, clearing any existing SearchItems.");
                SearchItems.Clear();
                return;
            }

            if (typeof(TItem) == typeof(string))
            {
                SearchItems = Data.Where(i => i != null).Select(i => new SearchItem<TItem>
                {
                    Text = (i as string)!,
                    DataObject = i,
                    Key = GetKeyValueOrDefault(i)
                }).ToList();
            }
            else
            {
                if (TextField == null)
                {
                    throw new ArgumentNullException(nameof(TextField));
                }

                SearchItems = Data.Where(i => i != null).Select(i => new SearchItem<TItem>
                {
                    Text = i?.GetType()?.GetProperty(TextField)?.GetValue(i, null)?.ToString() ?? "",
                    DataObject = i!,
                    Key = GetKeyValueOrDefault(i!)
                }).ToList();
            }

            if (RecentRepository != null)
            {
                foreach (var item in SearchItems)
                {
                    Console.WriteLine($"SearchItem Key: {item.Key}");

                    if (item.Key != null)
                    {
                        item.LastHit = await RecentRepository.GetHitsForKey(item.Key);
                    }

                }
            }

            Debug.WriteLine("Initialized {0} SearchItems", SearchItems.Count);

            Calculate();
        }

        string GetKeyValueOrDefault(TItem item)
        {
            string? key = null;
            if (string.IsNullOrWhiteSpace(KeyField))
            {
                if (item is string stringItem && RecentRepository != null)
                {
                    key = stringItem;
                }
            }
            else
            {
                key = typeof(TItem).GetProperty(KeyField)?.GetValue(item, null)?.ToString() ?? Guid.NewGuid().ToString();
            }
            Debug.WriteLine(key);

            return key ?? Guid.NewGuid().ToString();
        }

        void ClearInputValue()
        {
            lastSelectedItem = null;
            InputValue = "";

            if (isFocused)
            {
                // Go ahead and close down if we don't open just for focus
                isOpen = OpenOnFocus;
            }

            if (OnItemSelected.HasDelegate)
            {
                OnItemSelected.InvokeAsync(null!);
            }

            Calculate();
        }

        bool ShouldItemShow(bool isMatch, int showingIndex)
        {
            // If we're not open, don't show anything
            if (!isOpen)
            {
                return false;
            }

            // If MaxItemsToShow has a limit and we are above that, never show
            if (MaxItemsToShow != 0 && showingIndex >= MaxItemsToShow)
            {
                return false;
            }

            // State is Open + In Limit + Matching, show it
            if (isMatch)
            {
                return true;
            }

            // State Open + In Limit + NOT Matching
            // If we DON'T have an input value, we show the default list
            if (!hasInputValue)
            {
                return true;
            }

            return false;
        }

        #endregion METHODS
    }
}
