﻿using System.Collections.Generic;
using System.IO;
using System.Xml;
using SFPackager.Helpers;
using SFPackager.Models;

namespace SFPackager.Services
{
    public class SfProjectHandler
    {
        private readonly ManifestParser _appManifestHandler;

        public SfProjectHandler(ManifestParser appManifestHandler)
        {
            _appManifestHandler = appManifestHandler;
        }

        public ServiceFabricApplicationProject Parse(ServiceFabricApplicationProject sfProject, string srcBasePath, BaseConfig baseConfig)
        {
            var basePath = FileHelper.RemoveFileFromPath(sfProject.ProjectFileFullPath);

            using (var fileStream = new FileStream(sfProject.ProjectFileFullPath, FileMode.Open))
            using (var reader = XmlReader.Create(fileStream))
            {
                var document = new XmlDocument();
                document.Load(reader);
                var manager = new XmlNamespaceManager(document.NameTable);
                manager.AddNamespace("x", "http://schemas.microsoft.com/developer/msbuild/2003");

                sfProject.ApplicationManifestPath = ExtractApplicationManifest(basePath, document, manager);
                sfProject.BasePath = srcBasePath;

                sfProject = _appManifestHandler.ReadXml(sfProject);

                sfProject.Services = ExtractProjectReferences(basePath, sfProject.BuildOutputPathSuffix, baseConfig, document, manager);

                return sfProject;
            }
        }

        private static string ExtractApplicationManifest(string basePath, XmlNode document,
            XmlNamespaceManager namespaceManager)
        {
            var contents = document.SelectNodes("//x:Content/@Include", namespaceManager);

            foreach (var content in contents)
            {
                if (!(content is XmlAttribute))
                    continue;

                var attr = content as XmlAttribute;
                if (!attr.Value.ToLowerInvariant().Contains("applicationmanifest"))
                    continue;

                var path = FileHelper.RemoveFileFromPath(attr.Value);
                return Path.GetFullPath($"{basePath}{path}");
            }

            return string.Empty;
        }

        private Dictionary<string, ServiceFabricServiceProject> ExtractProjectReferences(
            string basePath,
            string buildOutputPathSuffix,
            BaseConfig baseConfig,
            XmlNode document,
            XmlNamespaceManager namespaceManager)
        {
            var projectReferences = new Dictionary<string, ServiceFabricServiceProject>();

            var projects = document.SelectNodes("//x:ProjectReference/@Include", namespaceManager);

            foreach (var service in projects)
            {
                if (!(service is XmlAttribute))
                    continue;

                var attr = service as XmlAttribute;
                var path = FileHelper.RemoveFileFromPath(attr.Value);
                var absolutePath = Path.GetFullPath($"{basePath}{path}");
                var projectFolder = FileHelper.RemoveFileFromPath(absolutePath);
                var projectFile = FileHelper.RemovePathFromPath(attr.Value);

                var serviceProject = new ServiceFabricServiceProject
                {
                    PackageRoot = $"{projectFolder}PackageRoot\\",
                    ProjectFolder = projectFolder,
                    ProjectFile = projectFile
                };

                // Ugly ASP.Net hack for now
                var buildOutputPath = projectFile.EndsWith(".xproj")
                    ? $"{projectFolder}bin\\{baseConfig.BuildConfiguration}\\net451"
                    : $"{projectFolder}{buildOutputPathSuffix}";

                var projectInfo = _appManifestHandler.ReadXml(serviceProject, buildOutputPath);

                projectInfo.IsAspNetCore = projectFile.EndsWith(".xproj");
                projectReferences.Add(projectInfo.ServiceName, projectInfo);
            }

            return projectReferences;
        }
    }
}