using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using NSpec;
using NSpec.Domain;
using NSpec.Domain.Formatters;

namespace Giropay.Tests.Unit
{
    public class Program
    {
        static void Main(string[] args)
        {
            string tags;
            IFormatter formatter;
            if(args.Any() && args[0] == "--xml-report")
            {
                tags = args.Length > 1 ? args[1] : "";

                //writes test results in an xunit compatbile xml format
                //can be imported in team city with Report Type set to "Ant JUnit" 
                //(yeah, not intuitive)
                var name = Assembly.GetEntryAssembly().GetName().Name;
                formatter = new XUnitFormatter();
                formatter.Options.Add("file", $"{name}-results.xml");
            }
            else
            {
                tags = args.FirstOrDefault() ?? "";
                formatter = new ConsoleFormatter();
            }

            var types = Assembly.GetEntryAssembly().GetTypes();
            var finder = new SpecFinder(types, "");
            var tagsFilter = new Tags().Parse(tags);
            var builder = new ContextBuilder(finder, tagsFilter, new DefaultConventions());
            
            var runner = new ContextRunner(tagsFilter, formatter, false);
            var results = runner.Run(builder.Contexts().Build());

            if(results.Failures().Count() > 0)
            {
                Environment.Exit(1);
            }
        }
    }
}