// <copyright file="WebConfigTransformTest.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace OpenTelemetry.Instrumentation.AspNet.Tests
{
    using System.IO;
    using System.Xml.Linq;
    using Microsoft.Web.XmlTransform;
    using Xunit;

    public class WebConfigTransformTest
    {
        private const string InstallConfigTransformationResourceName = "OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule.Tests.Resources.web.config.install.xdt";
        private const string UninstallConfigTransformationResourceName = "OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule.Tests.Resources.web.config.uninstall.xdt";

        [Fact]
        public void VerifyInstallationToBasicWebConfig()
        {
            const string OriginalWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules />
                    </system.web>
                    <system.webServer>
                        <modules />
                    </system.webServer>
                </configuration>";

            const string ExpectedWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules>
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                        </httpModules >
                    </system.web>
                    <system.webServer>
                        <modules>
                           <remove name=""TelemetryHttpModule"" />
                           <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler""/>
                        </modules>
                        <validation validateIntegratedModeConfiguration=""false"" />
                    </system.webServer>
                </configuration>";

            var transformedWebConfig = this.ApplyInstallTransformation(OriginalWebConfigContent, InstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyUpdateWithTypeRenamingWebConfig()
        {
            const string OriginalWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules>
                            <add name=""TelemetryHttpModuleSomeOldName"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                        </httpModules>
                    </system.web>
                    <system.webServer>
                        <modules>
                           <add name=""TelemetryHttpModuleSomeOldName"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler""/>
                        </modules>
                    </system.webServer>
                </configuration>";

            const string ExpectedWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules>
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                        </httpModules>
                    </system.web>
                    <system.webServer>
                        <modules>
                           <remove name=""TelemetryHttpModule"" />
                           <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler""/>
                        </modules>
                        <validation validateIntegratedModeConfiguration=""false"" />
                    </system.webServer>
                </configuration>";

            var transformedWebConfig = this.ApplyInstallTransformation(OriginalWebConfigContent, InstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyUpdateNewerVersionWebConfig()
        {
            const string OriginalWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules>
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                        </httpModules >
                    </system.web>
                    <system.webServer>
                        <modules>
                           <add name=""TelemetryHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"" preCondition=""managedHandler""/>
                        </modules>
                    </system.webServer>
                </configuration>";

            const string ExpectedWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules>
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                        </httpModules >
                    </system.web>
                    <system.webServer>
                        <modules>
                           <add name=""TelemetryHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"" preCondition=""managedHandler""/>
                           <remove name=""TelemetryHttpModule"" />
                           <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler""/>
                        </modules>
                        <validation validateIntegratedModeConfiguration=""false"" />
                    </system.webServer>
                </configuration>";

            var transformedWebConfig = this.ApplyInstallTransformation(OriginalWebConfigContent, InstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyUpdateWithIntegratedModeWebConfig()
        {
            const string OriginalWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules>
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                        </httpModules >
                    </system.web>
                    <system.webServer>
                        <validation validateIntegratedModeConfiguration=""false"" />
                        <modules>
                           <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""integratedMode,managedHandler""/>
                        </modules>
                    </system.webServer>
                </configuration>";

            const string ExpectedWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules>
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                        </httpModules >
                    </system.web>
                    <system.webServer>
                        <validation validateIntegratedModeConfiguration=""false"" />
                        <modules>
                           <remove name=""TelemetryHttpModule"" />
                           <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler""/>
                        </modules>
                    </system.webServer>
                </configuration>";

            var transformedWebConfig = this.ApplyInstallTransformation(OriginalWebConfigContent, InstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyUninstallationWithBasicWebConfig()
        {
            const string OriginalWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules>
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                        </httpModules >
                    </system.web>
                    <system.webServer>
                        <modules>
                           <remove name=""TelemetryHttpModule"" />
                           <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""integratedMode,managedHandler""/>
                        </modules>
                    </system.webServer>
                </configuration>";

            const string ExpectedWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules></httpModules>
                    </system.web>
                    <system.webServer>
                        <modules></modules>
                    </system.webServer>
                </configuration>";

            var transformedWebConfig = this.ApplyUninstallTransformation(OriginalWebConfigContent, UninstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyUninstallWithIntegratedPrecondition()
        {
            const string OriginalWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules>
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                        </httpModules>
                    </system.web>
                    <system.webServer>
                        <modules>
                           <remove name=""TelemetryHttpModule"" />
                           <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""integratedMode,managedHandler""/>
                        </modules>
                    </system.webServer>
                </configuration>";

            const string ExpectedWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules></httpModules>
                    </system.web>
                    <system.webServer>
                        <modules></modules>
                    </system.webServer>
                </configuration>";

            var transformedWebConfig = this.ApplyUninstallTransformation(OriginalWebConfigContent, UninstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyUninstallationWithUserModules()
        {
            const string OriginalWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules>
                            <add name=""UserModule"" type=""UserNamespace.WebModuleFoo""/>
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                        </httpModules>
                    </system.web>
                    <system.webServer>
                        <modules runAllManagedModulesForAllRequests=""true"">
                           <remove name=""TelemetryHttpModule"" />
                           <add name=""UserModule"" type=""UserNamespace.WebModuleFoo""/>
                           <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler""/>
                        </modules>
                    </system.webServer>
                </configuration>";

            const string ExpectedWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules>
                            <add name=""UserModule"" type=""UserNamespace.WebModuleFoo""/>
                        </httpModules >
                    </system.web>
                    <system.webServer>
                        <modules runAllManagedModulesForAllRequests=""true"">
                           <add name=""UserModule"" type=""UserNamespace.WebModuleFoo""/>
                        </modules>
                    </system.webServer>
                </configuration>";

            var transformedWebConfig = this.ApplyUninstallTransformation(OriginalWebConfigContent, UninstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyInstallationToWebConfigWithUserModules()
        {
            const string OriginalWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules>
                            <add name=""UserModule"" type=""UserNamespace.WebModuleFoo""/>
                        </httpModules>
                    </system.web>
                    <system.webServer>
                        <modules runAllManagedModulesForAllRequests=""true"">
                           <add name=""UserModule"" type=""UserNamespace.WebModuleFoo""/>
                        </modules>
                    </system.webServer>
                </configuration>";

            const string ExpectedWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules>
                            <add name=""UserModule"" type=""UserNamespace.WebModuleFoo""/>
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                        </httpModules>
                    </system.web>
                    <system.webServer>
                        <modules runAllManagedModulesForAllRequests=""true"">
                           <add name=""UserModule"" type=""UserNamespace.WebModuleFoo""/>
                           <remove name=""TelemetryHttpModule"" />
                           <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler""/>
                        </modules>
                        <validation validateIntegratedModeConfiguration=""false"" />
                    </system.webServer>
                </configuration>";

            var transformedWebConfig = this.ApplyInstallTransformation(OriginalWebConfigContent, InstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyInstallationToEmptyWebConfig()
        {
            const string OriginalWebConfigContent = @"<configuration/>";

            const string ExpectedWebConfigContent = @"
                <configuration>
                    <system.web>
                        <httpModules>
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                        </httpModules>
                    </system.web>
                    <system.webServer>
                        <validation validateIntegratedModeConfiguration=""false"" />
                        <modules>
                           <remove name=""TelemetryHttpModule"" />
                           <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler""/>
                        </modules>
                    </system.webServer>
                </configuration>";

            var transformedWebConfig = this.ApplyInstallTransformation(OriginalWebConfigContent, InstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyInstallationToWebConfigWithoutModules()
        {
            const string OriginalWebConfigContent = @"<configuration><system.webServer/></configuration>";

            const string ExpectedWebConfigContent = @"
                <configuration>
                    <system.webServer>
                        <validation validateIntegratedModeConfiguration=""false"" />
                        <modules>
                           <remove name=""TelemetryHttpModule"" />
                           <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler""/>
                        </modules>
                    </system.webServer>
                    <system.web>
                        <httpModules>
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                        </httpModules>
                    </system.web>
                </configuration>";

            var transformedWebConfig = this.ApplyInstallTransformation(OriginalWebConfigContent, InstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        private XDocument ApplyInstallTransformation(string originalConfiguration, string resourceName)
        {
            return this.ApplyTransformation(originalConfiguration, resourceName);
        }

        private XDocument ApplyUninstallTransformation(string originalConfiguration, string resourceName)
        {
            return this.ApplyTransformation(originalConfiguration, resourceName);
        }

        private void VerifyTransformation(string expectedConfigContent, XDocument transformedWebConfig)
        {
            Assert.True(
                XNode.DeepEquals(
                    transformedWebConfig.FirstNode,
                    XDocument.Parse(expectedConfigContent).FirstNode));
        }

        private XDocument ApplyTransformation(string originalConfiguration, string transformationResourceName)
        {
            XDocument result;
            Stream stream = null;
            try
            {
                stream = typeof(WebConfigTransformTest).Assembly.GetManifestResourceStream(transformationResourceName);
                var document = new XmlTransformableDocument();
                using (var transformation = new XmlTransformation(stream, null))
                {
                    stream = null;
                    document.LoadXml(originalConfiguration);
                    transformation.Apply(document);
                    result = XDocument.Parse(document.OuterXml);
                }
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
            }

            return result;
        }
    }
}
