// <copyright file="WebConfigWithLocationTagTransformTest.cs" company="OpenTelemetry Authors">
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

    public class WebConfigWithLocationTagTransformTest
    {
        private const string InstallConfigTransformationResourceName = "OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule.Tests.Resources.web.config.install.xdt";

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
                          <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                        </httpModules >
                      </system.web>
                      <system.webServer>
                        <validation validateIntegratedModeConfiguration=""false"" />
                        <modules>
                          <remove name=""TelemetryHttpModule"" />
                          <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler"" />
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
                                <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                              </httpModules >
                            </system.web>
                            <system.webServer>
                                <modules>
                                    <add name=""abc"" type=""type"" />
                                    <remove name=""TelemetryHttpModule"" />
                                    <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler"" />
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
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                          </httpModules >
                        </system.web>
                        <system.webServer>
                          <modules>
                            <add name=""abc"" type=""type"" />
                            <remove name=""TelemetryHttpModule"" />
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler"" />
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
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                          </httpModules>
                        </system.web>
                        <system.webServer>
                          <modules>
                            <add name=""abc"" type=""type"" />
                            <remove name=""TelemetryHttpModule"" />
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler"" />
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
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                          </httpModules>
                        </system.web>
                        <system.webServer>
                          <validation validateIntegratedModeConfiguration=""false"" />
                          <modules>
                            <remove name=""TelemetryHttpModule"" />
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler"" />
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
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                          </httpModules>
                        </system.web>
                        <system.webServer>
                          <validation validateIntegratedModeConfiguration=""false"" />
                          <modules>
                            <remove name=""TelemetryHttpModule"" />
                            <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler"" />
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
                              <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                          </httpModules>
                      </system.web>
                      <system.webServer>
                        <modules>
                          <add name=""abc"" type=""type"" />
                          <remove name=""TelemetryHttpModule"" />
                          <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler"" />
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
                              <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" />
                          </httpModules>
                      </system.web>
                      <system.webServer>
                        <modules>
                          <add name=""abc"" type=""type"" />
                          <remove name=""TelemetryHttpModule"" />
                          <add name=""TelemetryHttpModule"" type=""OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule, OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"" preCondition=""managedHandler"" />
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
                using var transformation = new XmlTransformation(stream, null);
                stream = null;
                document.LoadXml(originalConfiguration);
                transformation.Apply(document);
                result = XDocument.Parse(document.OuterXml);
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
