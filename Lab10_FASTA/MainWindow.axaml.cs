using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;


namespace Lab10_FASTA
{
    public partial class MainWindow : Window
    {
        private List<FastaSequence> sekwencje = new();
        public MainWindow()
        {
            InitializeComponent();
        }
        private async void OpenFile_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                AllowMultiple = true,
                Filters = new List<FileDialogFilter> { new() { Name = "FASTA", Extensions = { "fasta", "fa" } } }
            };

            var files = await dialog.ShowAsync(this);
            if (files == null) return;

            sekwencje.Clear();

            foreach (var file in files)
            {
                var lines = await File.ReadAllLinesAsync(file);
                Podziel(lines);
            }

            SequenceList.ItemsSource = sekwencje.Select(s => $"{s.Header} ({s.Sequence.Length} nt)");
            RenderBarChart();
        }

        private void SequenceList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var index = SequenceList.SelectedIndex;
            if (index < 0 || index >= sekwencje.Count) return;

            var seq = sekwencje[index];
            HeaderText.Text = $"Nag³ówek: {seq.Header}";
            LengthText.Text = $"D³ugoœæ: {seq.Sequence.Length}";
            GCText.Text = $"Zawartoœæ GC%: {seq.LiczGC():F2}%";
            CodonText.Text = $"Liczba kodonów: {seq.Sequence.Length / 3}";
            var counts = seq.Licz();
            ATGCText.Text = $"A: {counts['A']}, T: {counts['T']}, G: {counts['G']}, C: {counts['C']}";
        }

        private void Podziel(string[] lines)
        {
            string? header = null;
            string sequence = "";

            foreach (var line in lines)
            {
                if (line.StartsWith(">"))
                {
                    if (header != null)
                        sekwencje.Add(new FastaSequence(header, sequence));

                    header = line[1..].Trim();
                    sequence = "";
                }
                else
                {
                    sequence += line.Trim().ToUpper();
                }
            }

            if (header != null)
                sekwencje.Add(new FastaSequence(header, sequence));
        }
        private void RenderBarChart()
        {
            Wykres.Children.Clear();

            if (sekwencje.Count == 0) return;

            int maxLength = sekwencje.Max(s => s.Sequence.Length);
            double maxHeight = 150.0; 

            foreach (var seq in sekwencje)
            {
                double height = (seq.Sequence.Length / (double)maxLength) * maxHeight;

                var bar = new StackPanel
                {
                    Width = 40,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom
                };

                bar.Children.Add(new Border
                {
                    Height = height,
                    Background = Avalonia.Media.Brushes.SteelBlue,
                    Width = 30,
                    CornerRadius = new Avalonia.CornerRadius(4),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                });

                bar.Children.Add(new TextBlock
                {
                    Text = seq.Header.Length > 8 ? seq.Header[..8] + "…" : seq.Header,
                    FontSize = 10,
                    TextAlignment = Avalonia.Media.TextAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                });

                Wykres.Children.Add(bar);
            }
        }

        private async void ExportCSV_Click(object? sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filters = new List<FileDialogFilter> { new() { Name = "CSV", Extensions = { "csv" } } }
            };
            var file = await sfd.ShowAsync(this);
            if (file == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("Naglowek,Dlugosc,Liczba kodonow,GC%,A,T,G,C");

            foreach (var seq in sekwencje)
            {
                var counts = seq.Licz();
                sb.AppendLine($"{Escape(seq.Header)},{seq.Sequence.Length},{seq.Sequence.Length / 3}," +
                              $"{seq.LiczGC():F2}% ,{counts['A']},{counts['T']},{counts['G']},{counts['C']}");
            }

            await File.WriteAllTextAsync(file, sb.ToString());

            string Escape(string input) =>
                $"\"{input.Replace("\"", "\"\"")}\"";
        }
        private async void ExportJSON_Click(object? sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filters = new List<FileDialogFilter> { new() { Name = "JSON", Extensions = { "json" } } }
            };
            var file = await sfd.ShowAsync(this);
            if (file == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("[");

            for (int i = 0; i < sekwencje.Count; i++)
            {
                var seq = sekwencje[i];
                var counts = seq.Licz();

                sb.AppendLine("  {");
                sb.AppendLine($"    \"Naglowiek\": \"{Escape(seq.Header)}\",");
                sb.AppendLine($"    \"Dlugosc\": {seq.Sequence.Length},");
                sb.AppendLine($"    \"Liczba Kodonow\": {seq.Sequence.Length / 3},");
                sb.AppendLine($"    \"GC\": {seq.LiczGC():F2}%,");
                sb.AppendLine($"    \"A\": {counts['A']},");
                sb.AppendLine($"    \"T\": {counts['T']},");
                sb.AppendLine($"    \"G\": {counts['G']},");
                sb.AppendLine($"    \"C\": {counts['C']}");
                sb.Append("  }");
                if (i < sekwencje.Count - 1)
                    sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("]");

            await File.WriteAllTextAsync(file, sb.ToString());

            string Escape(string input) =>
                input.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

       

        public class FastaSequence
        {
            public string Header { get; }
            public string Sequence { get; }

            public FastaSequence(string header, string sequence)
            {
                Header = header;
                Sequence = sequence;
            }

            public double LiczGC()
            {
                if (Sequence.Length == 0) return 0;
                int gc = Sequence.Count(c => c == 'G' || c == 'C');
                return 100.0 * gc / Sequence.Length;
            }

            public Dictionary<char, int> Licz()
            {
                return new[] { 'A', 'T', 'G', 'C' }
                    .ToDictionary(c => c, c => Sequence.Count(x => x == c));
            }
        }
    }
}