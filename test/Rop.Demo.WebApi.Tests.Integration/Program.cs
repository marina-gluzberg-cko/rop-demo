using System;
using System.Linq;
using System.Reflection;
using NSpec;
using NSpec.Domain;
using NSpec.Domain.Formatters;
using Oakton;
using Microsoft.Extensions.Configuration;

namespace Rop.Demo.WebApi.Tests.Integration
{
    public class Program
    {
        static int Main(string[] args)
        {
            return CommandExecutor.ExecuteCommand<RunCommand>(args);
        }

        [Description("Run tests")]
        public class RunCommand : OaktonCommand<RunnerOptions>
        {
            public RunCommand()
            {
                Usage("Run all tests");
                Usage("Run a specific test class")
                    .Arguments(x => x.TestClass);
            }

            public override bool Execute(RunnerOptions options)
            {
                var envName = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                Console.WriteLine($"Environment: {envName}");

                var types = Assembly.GetEntryAssembly().GetTypes();
                var finder = new SpecFinder(types, options.TestClass);
                var tagsFilter = new Tags().Parse(options.TagsFlag);
                var builder = new ContextBuilder(finder, tagsFilter, new DefaultConventions());
                var runner = new ContextRunner(tagsFilter, GetFormatter(options), false);

                var results = runner.Run(builder.Contexts().Build());

                return !results.Failures().Any();
            }

            private IFormatter GetFormatter(RunnerOptions options)
            {
                switch (options.FormatterFlag)
                {
                    case Formatter.XUnit:
                        var formatter = new XUnitFormatter();
                        formatter.Options.Add("file", options.GetOutputFileName("xml"));
                        return formatter;
                    default:
                        return new ConsoleFormatter();
                }
            }
        }

        public class RunnerOptions
        {
            [Description("The test class to run")]
            public string TestClass { get; set; } = string.Empty;

            [Description("Comma-separated list of tags")]
            public string TagsFlag { get; set; }

            [Description("The formatter to use for test results. Defaults to Console")]
            public Formatter FormatterFlag { get; set; } = Formatter.Console;

            [Description("The output file for test results, if supported by the formatter. Default to {AssemblyName}-results.{ext}")]
            public string ResultsFileFlag { get; set; }

            [Description("The instance of chromium to use for browser automatization. Spawns its own instance if not specified.")]
            public string ChromiumUrlFlag { get; set; } = string.Empty;

            public string GetOutputFileName(string extension)
            {
                if (!string.IsNullOrEmpty(ResultsFileFlag))
                    return ResultsFileFlag;

                var name = Assembly.GetEntryAssembly().GetName().Name;
                return $"{name}-results.{extension}";
            }
        }

        public enum Formatter
        {
            Console,
            XUnit
        }
    }
}