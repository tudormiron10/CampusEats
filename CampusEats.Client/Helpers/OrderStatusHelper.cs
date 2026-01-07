using System.Text.RegularExpressions;

namespace CampusEats.Client.Helpers;

/// <summary>
/// Centralized helper for order status formatting, styling, and icons.
/// Uses Dictionary + Record pattern for single source of truth per status.
/// </summary>
public static partial class OrderStatusHelper
{
    /// <summary>
    /// All styling information for a given order status.
    /// </summary>
    public record StatusStyle(
        string BadgeClass,
        string HeaderClass,
        string IconBgClass,
        string Icon
    );

    private static readonly StatusStyle DefaultStyle = new(
        BadgeClass: "bg-slate-100 text-slate-600 border border-slate-200",
        HeaderClass: "bg-slate-50 border-b border-slate-100",
        IconBgClass: "bg-slate-200",
        Icon: """<svg class="w-6 h-6 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8.228 9c.549-1.165 2.03-2 3.772-2 2.21 0 4 1.343 4 3 0 1.4-1.278 2.575-3.006 2.907-.542.104-.994.54-.994 1.093m0 3h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path></svg>"""
    );

    private static readonly Dictionary<string, StatusStyle> Styles = new()
    {
        ["Pending"] = new(
            BadgeClass: "bg-amber-100 text-amber-700 border border-amber-200",
            HeaderClass: "bg-gradient-to-r from-amber-50 to-orange-50 border-b border-amber-100",
            IconBgClass: "bg-amber-100",
            Icon: """<svg class="w-6 h-6 text-amber-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"></path></svg>"""
        ),
        ["InPreparation"] = new(
            BadgeClass: "bg-blue-100 text-blue-700 border border-blue-200",
            HeaderClass: "bg-gradient-to-r from-blue-50 to-indigo-50 border-b border-blue-100",
            IconBgClass: "bg-blue-100",
            Icon: """<svg class="w-6 h-6 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 18.657A8 8 0 016.343 7.343S7 9 9 10c0-2 .5-5 2.986-7C14 5 16.09 5.777 17.656 7.343A7.975 7.975 0 0120 13a7.975 7.975 0 01-2.343 5.657z"></path></svg>"""
        ),
        ["Ready"] = new(
            BadgeClass: "bg-emerald-100 text-emerald-700 border border-emerald-200",
            HeaderClass: "bg-gradient-to-r from-emerald-50 to-teal-50 border-b border-emerald-100",
            IconBgClass: "bg-emerald-100",
            Icon: """<svg class="w-6 h-6 text-emerald-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"></path></svg>"""
        ),
        ["Completed"] = new(
            BadgeClass: "bg-slate-100 text-slate-600 border border-slate-200",
            HeaderClass: "bg-slate-50 border-b border-slate-100",
            IconBgClass: "bg-slate-200",
            Icon: """<svg class="w-6 h-6 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path></svg>"""
        ),
        ["Cancelled"] = new(
            BadgeClass: "bg-red-100 text-red-700 border border-red-200",
            HeaderClass: "bg-red-50 border-b border-red-100",
            IconBgClass: "bg-red-100",
            Icon: """<svg class="w-6 h-6 text-red-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path></svg>"""
        )
    };

    /// <summary>
    /// Gets all styling for a status. Returns default style for unknown statuses.
    /// </summary>
    public static StatusStyle Get(string status) =>
        Styles.GetValueOrDefault(status, DefaultStyle);

    /// <summary>
    /// Formats status string with spaces (e.g., "InPreparation" -> "In Preparation").
    /// </summary>
    public static string FormatStatus(string status) =>
        StatusSplitRegex().Replace(status, "$1 $2");

    /// <summary>
    /// Generates a human-readable order number from GUID.
    /// </summary>
    public static string GetOrderNumber(Guid orderId) =>
        ((orderId.GetHashCode() & 0x7FFFFFFF) % 1000000).ToString("D6");

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex StatusSplitRegex();
}