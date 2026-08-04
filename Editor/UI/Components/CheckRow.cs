using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>
    ///     One check, as a left-aligned hierarchy: a head line (status icon + name + short status word),
    ///     then the check's own message, then its fix - each on its own full-width wrapped line.
    ///     The message used to sit in the head line's right-hand slot, bold and severity-colored. Validator
    ///     messages are sentences, so that slot wrapped into a ragged multi-line column fighting the name
    ///     for width, and a passing row with an empty message rendered a blank slot instead of "PASS".
    ///     Structure fixes both: the status slot now only ever holds one short word, and severity color
    ///     lives on the icon and that word alone, so the reading order is name → verdict → why → fix.
    /// </summary>
    static class CheckRow
    {
        internal static VisualElement Create(string label, ReadinessOutcome outcome, bool informational,
            string detail = null, string fix = null)
        {
            string status = StatusFor(outcome, informational);
            var row = new VisualElement();
            row.AddToClassList("sorolla-check-row");

            var head = new VisualElement();
            head.AddToClassList("sorolla-check-row-head");

            var icon = new Label(IconFor(status));
            icon.AddToClassList("sorolla-check-row-icon");
            icon.AddToClassList(ClassFor(status));
            head.Add(icon);

            var labelElement = new Label(label);
            labelElement.AddToClassList("sorolla-check-row-label");
            head.Add(labelElement);

            var statusElement = new Label(WordFor(status));
            statusElement.AddToClassList("sorolla-check-row-status");
            statusElement.AddToClassList(ClassFor(status));
            head.Add(statusElement);

            row.Add(head);

            if (!string.IsNullOrEmpty(detail))
                row.Add(SubLine(detail));
            if (!string.IsNullOrEmpty(fix))
            {
                Label fixLine = SubLine($"Fix: {fix}");
                fixLine.AddToClassList("sorolla-check-row-fix");
                row.Add(fixLine);
            }

            return row;
        }

        /// <summary>A wrapped line under a check's head line, indented to clear the icon column. Exposed so
        /// a caller appending its own extra line (a device-snapshot failure reason, a remedy control) lands
        /// on the same grid as the check's own detail instead of re-deriving the indent.</summary>
        internal static Label SubLine(string text)
        {
            var label = new Label(text);
            label.AddToClassList("sorolla-check-row-detail");
            return label;
        }

        static string StatusFor(ReadinessOutcome outcome, bool informational) =>
            informational ? "info" : outcome switch
            {
                ReadinessOutcome.Pass => "pass",
                ReadinessOutcome.Fail => "fail",
                ReadinessOutcome.Warn => "warn",
                _ => "wait",
            };

        static string IconFor(string status) => status switch
        {
            "pass" => "✓",
            "fail" => "✕",
            "warn" => "⚠",
            "info" => "ℹ",
            _ => "•",
        };

        static string WordFor(string status) => status switch
        {
            "pass" => "PASS",
            "fail" => "FAIL",
            "warn" => "WARN",
            "info" => "INFO",
            _ => "PENDING",
        };

        static string ClassFor(string status) => status switch
        {
            "pass" => "sorolla-check-pass",
            "warn" => "sorolla-check-warn",
            "fail" => "sorolla-check-fail",
            "info" => "sorolla-check-info",
            _ => "sorolla-check-wait",
        };
    }
}
