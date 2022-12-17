using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using NLog;

namespace Exceptions
{
    public class ConverterProgram
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        public static void Main(params string[] args)
        {
            try
            {
                var filenames = args.Any() ? args : new[] { "text.txt" };
                var settings = LoadSettings();
                ConvertFiles(filenames, settings);
            }
            catch (Exception e)
            {
                log.Error(e);
            }
        }

        private static void ConvertFiles(string[] filenames, Settings settings)
        {
            var tasks = filenames
                .Select(fn => Task.Run(() => ConvertFile(fn, settings)))
                .ToArray();
            Task.WaitAll(tasks);
        }

        private static Settings LoadSettings()
        {
            var serializer = new XmlSerializer(typeof(Settings));

            var content = string.Empty;

            try
            {
                content = File.ReadAllText("settings.xml");
            }
            catch (Exception e)
            {
                log.Error(e, "Файл настроек .* отсутствует.");
                return Settings.Default;
            }

            try
            {
                return (Settings)serializer.Deserialize(new StringReader(content));
            }
            catch (Exception e)
            {
                log.Error(e, "Не удалось прочитать файл настроек");
                return Settings.Default;
            }
        }

        private static void ConvertFile(string filename, Settings settings)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(settings.SourceCultureName);
            if (settings.Verbose)
            {
                log.Info("Processing file " + filename);
                log.Info("Source Culture " + Thread.CurrentThread.CurrentCulture.Name);
            }
            IEnumerable<string> lines;
            try
            {
                lines = PrepareLines(filename);
            }
            catch (Exception e)
            {
                log.Error(e, $"File {filename} not found");
                return;
            }

            IEnumerable<string> convertedLines;

            try
            {
                convertedLines = lines
                    .Select(ConvertLine)
                    .Select(s => s.Length + " " + s);
            }
            catch (Exception e)
            {
                log.Error(e, e.Message);
                return;
            }

            File.WriteAllLines(filename + ".out", convertedLines);
        }

        private static IEnumerable<string> PrepareLines(string filename)
        {
            var lineIndex = 0;
            foreach (var line in File.ReadLines(filename))
            {
                if (line == "") continue;
                yield return line.Trim();
                lineIndex++;
            }
            yield return lineIndex.ToString();
        }

        public static string ConvertLine(string arg)
        {
            var result = ConvertAsDateTime(arg);

            if (result.Success)
                return result.Object;

            result = ConvertAsDouble(arg);

            if (result.Success)
                return result.Object;

            result = ConvertAsCharIndexInstruction(arg);

            if (result.Success)
                return result.Object;

            throw new ArgumentException("Некорректная строка");

            //try
            //{
            //    return ConvertAsDateTime(arg);
            //}
            //catch
            //{
            //    try
            //    {
            //        return ConvertAsDouble(arg);
            //    }
            //    catch
            //    {
            //        return ConvertAsCharIndexInstruction(arg);
            //    }
            //}
        }

        private static Result ConvertAsCharIndexInstruction(string s)
        {
            var parts = s.Split();

            if (parts.Length < 2) return new Result { Success = false, Object = null };

            var result = new Result();

            int charIndex = 0;

            if (!(result.Success = int.TryParse(parts[0], out charIndex)))
                return new Result { Success = false, Object = null };

            //var charIndex = int.Parse(parts[0]);

            if ((charIndex < 0) || (charIndex >= parts[1].Length))
                return new Result { Success = false, Object = null }; ;
            var text = parts[1];

            return new Result { Success = true, Object = text[charIndex].ToString() };
            //return text[charIndex].ToString();
        }

        private static Result ConvertAsDateTime(string arg)
        {
            var result = new Result();

            if (result.Success = DateTime.TryParse(arg, out var parseResult))
                result.Object = parseResult.ToString(CultureInfo.InvariantCulture);

            return result;
            //return DateTime.Parse(arg).ToString(CultureInfo.InvariantCulture);
        }

        private static Result ConvertAsDouble(string arg)
        {
            var result = new Result();
            if ((result.Success = double.TryParse(arg, out var parseResult)))
                result.Object = parseResult.ToString(CultureInfo.InvariantCulture);
            return result;
            //return double.Parse(arg).ToString(CultureInfo.InvariantCulture);
        }

        class Result
        {
            public bool Success { get; set; }
            public string Object { get; set; }
        }
    }
}