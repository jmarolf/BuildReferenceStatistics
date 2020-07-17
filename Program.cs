using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Rendering;
using System.CommandLine.Rendering.Views;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Build.Logging.StructuredLogger;

namespace BuildReferenceStatistics {

    class Program {
        /// <summary>
        /// Prints out reference info from binlog
        /// </summary>
        /// <param name="invocationContext"></param>
        /// <param name="binlog">Path to the binlog file.</param>
        /// <param name="top">Number of references to list  (use '*' to show all).</param>
        static void Main(
            InvocationContext invocationContext,
            FileInfo binlog,
            string? top = null) {


            bool showMostReferencedAssemblies = false;
            int? numberOfItemsToShow = null;
            if (top is string && int.TryParse(top, out var number)) {
                numberOfItemsToShow = number;
                showMostReferencedAssemblies = true;
            } else if (top is string && top == "*") {
                numberOfItemsToShow = null;
                showMostReferencedAssemblies = true;
            }

            Console.WriteLine($"Reading in '{binlog.Name}'");
            var timer = Stopwatch.StartNew();
            var invocations = CompilerInvocationsReader.ReadInvocations(binlog.FullName).ToArray();
            Console.WriteLine($"Read '{binlog.Name}' in {timer.Elapsed:hh\\:mm\\:ss\\.ff}");
            var referenceHistogram = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var invocation in invocations) {
                var commandline = invocation.CommandLineArguments;
                var references = GetReferences(commandline);
                referenceHistogram.AddRange(references);
            }

            Console.WriteLine();
            Console.WriteLine($"Number of unique references: {referenceHistogram.Keys.Count}");
            Console.WriteLine();
            if (showMostReferencedAssemblies) {
                RenderReferenceTable(referenceHistogram, numberOfItemsToShow, invocationContext.Console);
                Console.WriteLine();
            }

            Console.WriteLine();
            RenderFrequencyTable(referenceHistogram, invocationContext.Console);
            Console.WriteLine();
        }

        private static void RenderFrequencyTable(Dictionary<string, int> referenceHistogram, IConsole console) {
            Dictionary<int, int> referenceCountHistogram = new Dictionary<int, int>();
            foreach (var (_, count) in referenceHistogram) {
                referenceCountHistogram.AddRange(count);
            }

            var normalizedFactor = (int)Math.Ceiling(referenceCountHistogram.Values.Max() / 50.0);
            var lines = referenceCountHistogram
                .GroupBy(kvp => kvp.Key)
                .OrderByDescending(x => x.Key)
                .SelectMany(x => x)
                .Where(x => x.Value / normalizedFactor > 1);

            var view = new TableView<KeyValuePair<int, int>> {
                Items = lines.ToArray()
            };

            view.AddColumn(cellValue: x => x.Key, header: new ContentView("# of References".Underline()));
            view.AddColumn(cellValue: x => string.Join("", Enumerable.Repeat("*", x.Value / normalizedFactor)), new ContentView("Frequency".Underline()));

            var consoleRenderer = new ConsoleRenderer(console, mode: console.GetTerminal().DetectOutputMode(), resetAfterRender: true);

            var screen = new ScreenView(renderer: consoleRenderer, console) { Child = view };
            screen.Render(new Region(0, 0, width: Console.WindowWidth, height: int.MaxValue));
            console.GetTerminal().ShowCursor();
        }

        private static void RenderReferenceTable(Dictionary<string, int> referenceHistogram, int? numberOfRows, IConsole console) {
            var lines = referenceHistogram
                .GroupBy(kvp => kvp.Value)
                .OrderByDescending(x => x.Key)
                .SelectMany(x => x);

            if (numberOfRows is int rows) {
                lines = lines.Take(rows);
            }

            var view = new TableView<KeyValuePair<string, int>> {
                Items = lines.ToArray()
            };
            view.AddColumn(cellValue: x => Path.GetFileName(x.Key).Replace("\"", ""), new ContentView("Assembly Name".Underline()));
            view.AddColumn(cellValue: x => x.Value, new ContentView("Times Projects Referenced This Assemby".Underline()));

            var consoleRenderer = new ConsoleRenderer(console, mode: console.GetTerminal().DetectOutputMode(), resetAfterRender: true);

            var screen = new ScreenView(renderer: consoleRenderer, console) { Child = view };
            screen.Render(new Region(0, 0, width: Console.WindowWidth, height: int.MaxValue));
            console.GetTerminal().ShowCursor();
        }

        private static string[] GetReferences(string commandline) {
            var arguments = commandline.Split("/reference:").Skip(1).ToArray();
            arguments[^1] = arguments[^1].Split("/")[0].Trim();
            return arguments;
        }
    }
}
