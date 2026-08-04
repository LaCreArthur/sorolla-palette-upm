using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>
    ///     One check, as a left-aligned hierarchy: a head line (status icon + name + short status word),
    ///     then each finding's evidence and its paired action - every one on its own full-width wrapped line.
    ///     The message used to sit in the head line's right-hand slot, bold and severity-colored. Validator
    ///     messages are sentences, so that slot wrapped into a ragged multi-line column fighting the name
    ///     for width, and a passing row with an empty message rendered a blank slot instead of "PASS".
    ///     Structure fixes both: the status slot now only ever holds one short word, and severity color
    ///     lives on the icon and that word alone, so the reading order is name → verdict → why → fix.
    /// </summary>
    static class CheckRow
    {
        /// <summary>The head line grades the row; every finding underneath keeps its own evidence and its
        /// own action, so a check that observed several problems never renders as one line with one fix.
        /// When there is more than one, each block names its own severity - a warning inside a failing row
        /// must not read as part of the failure.</summary>
        internal static VisualElement Create(string label, ReadinessRow model)
        {
            string status = StatusFor(model);
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

            bool several = model.Findings.Count > 1;
            foreach (ReadinessFinding finding in model.Findings)
            {
                string findingStatus = StatusFor(finding.Outcome, finding.Informational);
                if (!string.IsNullOrEmpty(finding.Evidence))
                    row.Add(SubLine(several
                        ? $"{WordFor(findingStatus)}: {finding.Evidence}"
                        : finding.Evidence));

                // When the evidence IS the instruction (a skip message on a row that required a verdict),
                // the finding carries the same text in both slots - print it once.
                if (string.IsNullOrEmpty(finding.Fix) || finding.Fix == finding.Evidence) continue;
                // A passing finding's action is a caveat about what the pass did not establish (valid
                // GameAnalytics keys do not prove the platform exists in the dashboard). Labelling it "Fix"
                // would put homework on a green row; dropping it would delete the only place a studio
                // learns the limit of the pass.
                Label actionLine = SubLine(finding.Outcome == ReadinessOutcome.Pass
                    ? $"Note: {finding.Fix}"
                    : $"Fix: {finding.Fix}");
                actionLine.AddToClassList("sorolla-check-row-fix");
                row.Add(actionLine);
            }

            return row;
        }

        /// <summary>A wrapped line under a check's head line, indented to clear the icon column.</summary>
        static Label SubLine(string text)
        {
            var label = new Label(text);
            label.AddToClassList("sorolla-check-row-detail");
            return label;
        }

        /// <summary>
        ///     A row with no findings NEVER voted: the gate does not apply to this project, or nothing
        ///     optional reported. Both carry the model's default Pass outcome, which is exactly why the
        ///     DISPOSITION decides the head line here - rendering "✓ PASS" for a row that was never checked
        ///     is the false green this whole surface exists to prevent (on any Android project the iOS
        ///     Firebase config row is that case).
        /// </summary>
        static string StatusFor(ReadinessRow row) => row.Disposition switch
        {
            ReadinessDisposition.NotApplicable => "na",
            ReadinessDisposition.OptionalSkipped => "unchecked",
            _ => StatusFor(row.Outcome, row.Informational),
        };

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
            "na" => "–",
            "unchecked" => "–",
            _ => "•",
        };

        static string WordFor(string status) => status switch
        {
            "pass" => "PASS",
            "fail" => "FAIL",
            "warn" => "WARN",
            "info" => "INFO",
            "na" => "N/A",
            "unchecked" => "NOT CHECKED",
            _ => "PENDING",
        };

        static string ClassFor(string status) => status switch
        {
            "pass" => "sorolla-check-pass",
            "warn" => "sorolla-check-warn",
            "fail" => "sorolla-check-fail",
            "info" => "sorolla-check-info",
            "na" => "sorolla-check-info",
            "unchecked" => "sorolla-check-info",
            _ => "sorolla-check-wait",
        };
    }
}
