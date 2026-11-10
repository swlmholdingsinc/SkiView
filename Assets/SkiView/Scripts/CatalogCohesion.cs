using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SkiView
{
    /// <summary>Exact catalog bundle names against files on disk. Does not rename anything.</summary>
    public static class CatalogCohesion
    {
        public const long MaxBundleBytes = 100L * 1024L * 1024L;

        static readonly Regex BundleId = new Regex(@"[A-Za-z0-9_.+\-]+\.bundle", RegexOptions.Compiled);

        public sealed class Report
        {
            public bool Proven;
            public string Reason;
            public readonly List<string> Missing = new List<string>();
        }

        public static Report Check(IEnumerable<string> catalogTexts, IEnumerable<string> fileNames)
        {
            var wanted = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            if (catalogTexts != null)
                foreach (var text in catalogTexts)
                    if (!string.IsNullOrEmpty(text))
                        foreach (Match m in BundleId.Matches(text))
                            wanted.Add(Path.GetFileName(m.Value));
            var report = new Report();
            if (wanted.Count == 0)
            {
                report.Proven = false;
                report.Reason = "no catalog .bundle ids. Reuse is not proven. Files were not renamed.";
                return report;
            }
            var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (fileNames != null)
                foreach (var f in fileNames)
                    if (!string.IsNullOrEmpty(f)) have.Add(Path.GetFileName(f));
            foreach (var id in wanted)
                if (!have.Contains(id)) report.Missing.Add(id);
            report.Proven = report.Missing.Count == 0;
            report.Reason = report.Proven
                ? "catalog ids match those exact file names"
                : "catalog ids are absent on disk. A hashed name is not the same file as the unhashed name. Files were not renamed.";
            return report;
        }
    }
}
