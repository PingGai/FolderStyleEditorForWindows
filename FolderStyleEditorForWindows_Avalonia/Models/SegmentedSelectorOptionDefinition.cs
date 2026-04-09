using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FolderStyleEditorForWindows.Controls;
using FolderStyleEditorForWindows.Services;

namespace FolderStyleEditorForWindows.Models;

public sealed class SegmentedSelectorOptionDefinition<TValue>
{
    public SegmentedSelectorOptionDefinition(
        TValue value,
        string key,
        string labelResourceKey,
        string descriptionResourceKey,
        string? tooltipResourceKey = null)
    {
        Value = value;
        Key = key;
        LabelResourceKey = labelResourceKey;
        DescriptionResourceKey = descriptionResourceKey;
        TooltipResourceKey = tooltipResourceKey;
    }

    public TValue Value { get; }

    public string Key { get; }

    public string LabelResourceKey { get; }

    public string DescriptionResourceKey { get; }

    public string? TooltipResourceKey { get; }
}

public static class SegmentedSelectorOptionDefinition
{
    public static ObservableCollection<LiquidSegmentedSelectorItem> BuildItems<TValue>(
        IEnumerable<SegmentedSelectorOptionDefinition<TValue>> definitions,
        LocalizationManager localizationManager)
    {
        return new ObservableCollection<LiquidSegmentedSelectorItem>(
            definitions.Select(definition => new LiquidSegmentedSelectorItem(
                definition.Key,
                localizationManager[definition.LabelResourceKey],
                definition.TooltipResourceKey is null ? null : localizationManager[definition.TooltipResourceKey])));
    }

    public static string ResolveDescription<TValue>(
        string? selectedKey,
        IEnumerable<SegmentedSelectorOptionDefinition<TValue>> definitions,
        LocalizationManager localizationManager,
        string fallbackResourceKey)
    {
        var matched = definitions.FirstOrDefault(definition =>
            string.Equals(definition.Key, selectedKey, StringComparison.Ordinal));

        return matched is null
            ? localizationManager[fallbackResourceKey]
            : localizationManager[matched.DescriptionResourceKey];
    }

    public static bool TryResolveValue<TValue>(
        string? selectedKey,
        IEnumerable<SegmentedSelectorOptionDefinition<TValue>> definitions,
        out TValue value)
    {
        var matched = definitions.FirstOrDefault(definition =>
            string.Equals(definition.Key, selectedKey, StringComparison.Ordinal));

        if (matched is null)
        {
            value = default!;
            return false;
        }

        value = matched.Value;
        return true;
    }

    public static string ResolveKey<TValue>(
        TValue value,
        IEnumerable<SegmentedSelectorOptionDefinition<TValue>> definitions,
        string fallbackKey)
    {
        var comparer = EqualityComparer<TValue>.Default;
        return definitions.FirstOrDefault(definition => comparer.Equals(definition.Value, value))?.Key ?? fallbackKey;
    }
}
