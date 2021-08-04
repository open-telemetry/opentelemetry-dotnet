// <copyright file="WebConfigWithLocationTagTransformTest.cs" company="Microsoft">
// Copyright (c) .NET Foundation. All rights reserved.
//
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// </copyright>

namespace Microsoft.AspNet.TelemetryCorrelation.Tests
{
    using System.IO;
    using System.Xml.Linq;
    using Microsoft.Web.XmlTransform;
    using Xunit;

    public class WebConfigWithLocationTagTransformTest
    {
        private const string InstallConfigTransformationResourceName = "Microsoft.AspNet.TelemetryCorrelation.Tests.Resources.web.config.install.xdt";

        [Fact]
        public void VerifyInstallationWhenNonGlobalLocationTagExists()
        {
            const string OriginalWebConfigContent = @"
                    <configuration> 
                      <location path=""a.aspx"">
                        <system.webServer>
                          <modules>
                            <add name=""abc"" type=""type"" />
                          </modules>
                        </system.webServer>
                      </location>
                    </configuration>";

            const string ExpectedWebConfigContent = @"
                    <configuration>
                      <location path=""a.aspx"">
                        <system.webServer>
                          <modules>
                            <add name=""abc"" type=""type"" />
                          </modules>
                        </system.webServer>
                      </location>
                      <system.web>
                        <httpModules>
                          <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" />
                        </httpModules >
                      </system.web>
                      <system.webServer>
                        <validation validateIntegratedModeConfiguration=""false"" />
                        <modules>
                          <remove name=""TelemetryCorrelationHttpModule"" />
                          <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" preCondition=""managedHandler"" />
                        </modules>
                      </system.webServer>
                    </configuration>";

            var transformedWebConfig = this.ApplyInstallTransformation(OriginalWebConfigContent, InstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyInstallationWhenGlobalAndNonGlobalLocationTagExists()
        {
            const string OriginalWebConfigContent = @"
                    <configuration> 
                        <location path=""a.aspx"">
                            <system.webServer>
                                <modules>
                                    <add name=""abc"" type=""type"" />
                                </modules>
                            </system.webServer>
                        </location>
                        <location path="".""> 
                            <system.web>
                              <httpModules>
                                <add name=""abc"" type=""type"" />
                              </httpModules >
                            </system.web>
                            <system.webServer>
                                <modules>
                                    <add name=""abc"" type=""type""/>
                                </modules>
                            </system.webServer>
                        </location> 
                    </configuration>";

            const string ExpectedWebConfigContent = @"
                    <configuration>
                        <location path=""a.aspx"">
                            <system.webServer>
                                <modules>
                                  <add name=""abc"" type=""type"" />
                                </modules>
                            </system.webServer>
                        </location>
                        <location path=""."">
                            <system.web>
                              <httpModules>
                                <add name=""abc"" type=""type"" />
                                <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" />
                              </httpModules >
                            </system.web>
                            <system.webServer>
                                <modules>
                                    <add name=""abc"" type=""type"" />
                                    <remove name=""TelemetryCorrelationHttpModule"" />
                                    <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" preCondition=""managedHandler"" />
                                </modules>
                                <validation validateIntegratedModeConfiguration=""false"" />
                            </system.webServer>
                        </location>
                        <system.web></system.web>
                        <system.webServer></system.webServer>
                    </configuration>";

            var transformedWebConfig = this.ApplyInstallTransformation(OriginalWebConfigContent, InstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyInstallationToLocationTagWithDotPathAndExistingModules()
        {
            const string OriginalWebConfigContent = @"
                    <configuration> 
                        <location path="".""> 
                            <system.web>
                              <httpModules>
                                <add name=""abc"" type=""type"" />
                              </httpModules >
                            </system.web>
                            <system.webServer>
                                <modules>
                                    <add name=""abc"" type=""type""/>
                                </modules>
                            </system.webServer>
                        </location> 
                        <system.webServer>
                        </system.webServer>
                    </configuration>";

            const string ExpectedWebConfigContent = @"
                    <configuration>
                      <location path=""."">
                        <system.web>
                          <httpModules>
                            <add name=""abc"" type=""type"" />
                            <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" />
                          </httpModules >
                        </system.web>
                        <system.webServer>
                          <modules>
                            <add name=""abc"" type=""type"" />
                            <remove name=""TelemetryCorrelationHttpModule"" />
                            <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" preCondition=""managedHandler"" />
                          </modules>
                          <validation validateIntegratedModeConfiguration=""false"" />
                        </system.webServer>
                      </location>
                      <system.webServer></system.webServer>
                      <system.web></system.web>
                    </configuration>";

            var transformedWebConfig = this.ApplyInstallTransformation(OriginalWebConfigContent, InstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyInstallationToLocationTagWithEmptyPathAndExistingModules()
        {
            const string OriginalWebConfigContent = @"
                    <configuration> 
                        <location>
                            <system.web>
                              <httpModules>
                                <add name=""abc"" type=""type"" />
                              </httpModules >
                            </system.web>
                            <system.webServer>
                                <modules>
                                    <add name=""abc"" type=""type""/>
                                </modules>
                            </system.webServer>
                        </location> 
                    </configuration>";

            const string ExpectedWebConfigContent = @"
                    <configuration>
                      <location>
                        <system.web>
                          <httpModules>
                            <add name=""abc"" type=""type"" />
                            <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" />
                          </httpModules>
                        </system.web>
                        <system.webServer>
                          <modules>
                            <add name=""abc"" type=""type"" />
                            <remove name=""TelemetryCorrelationHttpModule"" />
                            <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" preCondition=""managedHandler"" />
                          </modules>
                          <validation validateIntegratedModeConfiguration=""false"" />
                        </system.webServer>
                      </location>
                      <system.web></system.web>
                      <system.webServer></system.webServer>
                    </configuration>";

            var transformedWebConfig = this.ApplyInstallTransformation(OriginalWebConfigContent, InstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyInstallationToLocationTagWithDotPathWithNoModules()
        {
            const string OriginalWebConfigContent = @"
                    <configuration> 
                        <location path="".""> 
                            <system.web> 
                            </system.web> 
                            <system.webServer>
                            </system.webServer>
                        </location> 
                        <system.web> 
                        </system.web> 
                        <system.webServer>
                        </system.webServer>
                    </configuration>";

            const string ExpectedWebConfigContent = @"
                    <configuration>
                      <location path=""."">
                        <system.web> 
                          <httpModules>
                            <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" />
                          </httpModules>
                        </system.web> 
                        <system.webServer>
                          <validation validateIntegratedModeConfiguration=""false"" />
                          <modules>
                            <remove name=""TelemetryCorrelationHttpModule"" />
                            <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" preCondition=""managedHandler"" />
                          </modules>
                        </system.webServer>
                      </location>
                      <system.web>
                      </system.web>
                      <system.webServer>
                      </system.webServer>
                    </configuration>";

            var transformedWebConfig = this.ApplyInstallTransformation(OriginalWebConfigContent, InstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyInstallationToLocationTagWithEmptyPathWithNoModules()
        {
            const string OriginalWebConfigContent = @"
                    <configuration> 
                        <location> 
                            <system.web> 
                            </system.web> 
                            <system.webServer>
                            </system.webServer>
                        </location> 
                    </configuration>";

            const string ExpectedWebConfigContent = @"
                    <configuration>
                      <location>
                        <system.web>
                          <httpModules>
                            <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" />
                          </httpModules>
                        </system.web>
                        <system.webServer>
                          <validation validateIntegratedModeConfiguration=""false"" />
                          <modules>
                            <remove name=""TelemetryCorrelationHttpModule"" />
                            <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" preCondition=""managedHandler"" />
                          </modules>
                        </system.webServer>
                      </location>
                      <system.web>
                      </system.web>
                      <system.webServer>
                      </system.webServer>
                    </configuration>";

            var transformedWebConfig = this.ApplyInstallTransformation(OriginalWebConfigContent, InstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyInstallationToLocationTagWithDotPathWithGlobalModules()
        {
            const string OriginalWebConfigContent = @"
                    <configuration> 
                        <location path="".""> 
                            <system.web> 
                            </system.web> 
                            <system.webServer>
                            </system.webServer>
                        </location> 
                        <system.web> 
                            <httpModules> 
                                <add name=""abc"" type=""type"" /> 
                            </httpModules> 
                        </system.web> 
                        <system.webServer>
                            <modules>
                                <add name=""abc"" type=""type""/>
                            </modules>
                        </system.webServer>
                    </configuration>";

            const string ExpectedWebConfigContent = @"
                    <configuration>
                      <location path=""."">
                        <system.web>
                        </system.web>
                        <system.webServer>
                            <validation validateIntegratedModeConfiguration=""false"" />
                        </system.webServer>
                      </location>
                      <system.web> 
                          <httpModules> 
                              <add name=""abc"" type=""type"" /> 
                              <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" />
                          </httpModules> 
                      </system.web> 
                      <system.webServer>
                        <modules>
                          <add name=""abc"" type=""type"" />
                          <remove name=""TelemetryCorrelationHttpModule"" />
                          <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" preCondition=""managedHandler"" />
                        </modules>
                      </system.webServer>
                    </configuration>";

            var transformedWebConfig = this.ApplyInstallTransformation(OriginalWebConfigContent, InstallConfigTransformationResourceName);
            this.VerifyTransformation(ExpectedWebConfigContent, transformedWebConfig);
        }

        [Fact]
        public void VerifyInstallationToLocationTagWithEmptyPathWithGlobalModules()
        {
            const string OriginalWebConfigContent = @"
                    <configuration> 
                        <location> 
                        </location> 
                        <system.web> 
                          <httpModules> 
                            <add name=""abc"" type=""type"" /> 
                          </httpModules> 
                        </system.web> 
                        <system.webServer>
                            <modules>
                                <add name=""abc"" type=""type""/>
                            </modules>
                        </system.webServer>
                    </configuration>";

            const string ExpectedWebConfigContent = @"
                    <configuration>
                      <location>
                      </location>
                      <system.web> 
                          <httpModules> 
                              <add name=""abc"" type=""type"" /> 
                              <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" />
                          </httpModules> 
                      </system.web> 
                      <system.webServer>
                        <modules>
                          <add name=""abc"" type=""type"" />
                          <remove name=""TelemetryCorrelationHttpModule"" />
                          <add name=""TelemetryCorrelationHttpModule"" type=""Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation"" preCondition=""managedHandler"" />
                        </modules>
                        <validation validateIntegratedModeConfiguration=""false"" />
                      </system.webServer>
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
