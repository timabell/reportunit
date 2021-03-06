﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using RazorEngine;
using RazorEngine.Configuration;
using RazorEngine.Templating;
using RazorEngine.Text;

using ReportUnit.Logging;
using ReportUnit.Model;
using ReportUnit.Parser;

namespace ReportUnit
{
    class ReportUnitService
    {
        private const string ns = "ReportUnit.Parser";
        private Logger logger = Logger.GetLogger();

        public ReportUnitService() { }

        public void CreateFolderReport(string inputDirectory, string outputDirectory)
        {
            InitializeRazor();

            var filePathList = Directory.GetFiles(inputDirectory, "*.*", SearchOption.TopDirectoryOnly).ToList();

            var reportCollection = new Dictionary<Report, string>();
            var sidenavLinks = Templates.SideNav.GetIndexLink();
            var reportList = new List<Report>();

            foreach (var filePath in filePathList)
            {
                var testRunner = GetTestRunner(filePath);

                if (!(testRunner.Equals(TestRunner.Unknown)))
                {
                    IParser parser = (IParser)Assembly.GetExecutingAssembly().CreateInstance(ns + "." + Enum.GetName(typeof(TestRunner), testRunner));
                    var report = parser.Parse(filePath);
                    reportList.Add(report);

                    string reportHtml = Engine.Razor.RunCompile(Templates.File.GetSource(), "reportKey", typeof(Model.Report), report, null);

                    sidenavLinks += Engine.Razor.RunCompile(Templates.SideNav.GetSource(), "sidenavKey", typeof(Report), report, null);

                    reportCollection.Add(report, reportHtml);
                }
            }

            string summaryHtml = Engine.Razor.RunCompile(Templates.Summary.GetSource(), "summaryKey", typeof(List<Model.Report>), reportList, null); 
            File.WriteAllText(Path.Combine(outputDirectory, "Index.html"), summaryHtml.Replace("<!--%SIDENAV%-->", sidenavLinks));
            
            // sidenav links can only be known after all files are processed
            // some files may be invalid, so the entire input file collection may not be used
            // only valid processed files go here ->
            foreach (KeyValuePair<Report, string> entry in reportCollection)
            {
                File.WriteAllText(Path.Combine(outputDirectory, entry.Key.GetHtmlFileName()), entry.Value.Replace("<!--%SIDENAV%-->", sidenavLinks));
            }
        }

        public void CreateFileReport(string inputFile, string outputFile)
        {
            InitializeRazor();

            var testRunner = GetTestRunner(inputFile);

            string html = "";

            if (!(testRunner.Equals(TestRunner.Unknown)))
            {
                IParser parser = (IParser) Assembly.GetExecutingAssembly().CreateInstance(ns + "." + Enum.GetName(typeof(TestRunner), testRunner));
                var report = parser.Parse(inputFile);

                html = Engine.Razor.RunCompile(Templates.File.GetSource(), "reportKey", typeof(Model.Report), report, null);
            }

            File.WriteAllText(outputFile, html);
        }

        private TestRunner GetTestRunner(string inputFile)
        {
            var testRunner = new ParserFactory(inputFile).GetTestRunnerType();

            logger.Info("The file " + inputFile + " contains " + Enum.GetName(typeof(TestRunner), testRunner) + " test results");

            return testRunner;
        }

        private void InitializeRazor()
        {
            TemplateServiceConfiguration templateConfig = new TemplateServiceConfiguration();
            templateConfig.DisableTempFileLocking = true;
            templateConfig.EncodedStringFactory = new RawStringFactory();
            templateConfig.CachingProvider = new DefaultCachingProvider(x => { });
            var service = RazorEngineService.Create(templateConfig);
            Engine.Razor = service;
        }
    }
}
