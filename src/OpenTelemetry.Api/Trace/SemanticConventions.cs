// <copyright file="SemanticConventions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
    /// <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/semantic_conventions/README.md"/>.
    /// </summary>
    public static class SemanticConventions
    {
        // The set of constants matches the specification as of this commit.
        // https://github.com/open-telemetry/opentelemetry-specification/tree/709293fe132709705f0e0dd4252992e87a6ec899/specification/trace/semantic_conventions
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public const string AttributeServiceName = "service.name";
        public const string AttributeServiceNamespace = "service.namespace";
        public const string AttributeServiceInstance = "service.instance.id";
        public const string AttributeServiceVersion = "service.version";

        public const string AttributeTelemetrySDKName = "telemetry.sdk.name";
        public const string AttributeTelemetrySDKLanguage = "telemetry.sdk.language";
        public const string AttributeTelemetrySDKVersion = "telemetry.sdk.version";

        public const string AttributeContainerName = "container.name";
        public const string AttributeContainerImage = "container.image.name";
        public const string AttributeContainerTag = "container.image.tag";

        public const string AttributeFaasName = "faas.name";
        public const string AttributeFaasID = "faas.id";
        public const string AttributeFaasVersion = "faas.version";
        public const string AttributeFaasInstance = "faas.instance";

        public const string AttributeK8sCluster = "k8s.cluster.name";
        public const string AttributeK8sNamespace = "k8s.namespace.name";
        public const string AttributeK8sPod = "k8s.pod.name";
        public const string AttributeK8sDeployment = "k8s.deployment.name";

        public const string AttributeHostHostname = "host.hostname";
        public const string AttributeHostID = "host.id";
        public const string AttributeHostName = "host.name";
        public const string AttributeHostType = "host.type";
        public const string AttributeHostImageName = "host.image.name";
        public const string AttributeHostImageID = "host.image.id";
        public const string AttributeHostImageVersion = "host.image.version";

        public const string AttributeProcessID = "process.id";
        public const string AttributeProcessExecutableName = "process.executable.name";
        public const string AttributeProcessExecutablePath = "process.executable.path";
        public const string AttributeProcessCommand = "process.command";
        public const string AttributeProcessCommandLine = "process.command_line";
        public const string AttributeProcessUsername = "process.username";

        public const string AttributeCloudProvider = "cloud.provider";
        public const string AttributeCloudAccount = "cloud.account.id";
        public const string AttributeCloudRegion = "cloud.region";
        public const string AttributeCloudZone = "cloud.zone";
        public const string AttributeComponent = "component";

        public const string AttributeNetTransport = "net.transport";
        public const string AttributeNetPeerIP = "net.peer.ip";
        public const string AttributeNetPeerPort = "net.peer.port";
        public const string AttributeNetPeerName = "net.peer.name";
        public const string AttributeNetHostIP = "net.host.ip";
        public const string AttributeNetHostPort = "net.host.port";
        public const string AttributeNetHostName = "net.host.name";

        public const string AttributeEnduserID = "enduser.id";
        public const string AttributeEnduserRole = "enduser.role";
        public const string AttributeEnduserScope = "enduser.scope";

        public const string AttributePeerService = "peer.service";

        public const string AttributeHTTPMethod = "http.method";
        public const string AttributeHTTPURL = "http.url";
        public const string AttributeHTTPTarget = "http.target";
        public const string AttributeHTTPHost = "http.host";
        public const string AttributeHTTPScheme = "http.scheme";
        public const string AttributeHTTPStatusCode = "http.status_code";
        public const string AttributeHTTPStatusText = "http.status_text";
        public const string AttributeHTTPFlavor = "http.flavor";
        public const string AttributeHTTPServerName = "http.server_name";
        public const string AttributeHTTPHostName = "host.name";
        public const string AttributeHTTPHostPort = "host.port";
        public const string AttributeHTTPRoute = "http.route";
        public const string AttributeHTTPClientIP = "http.client_ip";
        public const string AttributeHTTPUserAgent = "http.user_agent";
        public const string AttributeHTTPRequestContentLength = "http.request_content_length";
        public const string AttributeHTTPRequestContentLengthUncompressed = "http.request_content_length_uncompressed";
        public const string AttributeHTTPResponseContentLength = "http.response_content_length";
        public const string AttributeHTTPResponseContentLengthUncompressed = "http.response_content_length_uncompressed";

        public const string AttributeDBSystem = "db.system";
        public const string AttributeDBConnectionString = "db.connection_string";
        public const string AttributeDBUser = "db.user";
        public const string AttributeDBMSSQLInstanceName = "db.mssql.instance_name";
        public const string AttributeDBJDBCDriverClassName = "db.jdbc.driver_classname";
        public const string AttributeDBName = "db.name";
        public const string AttributeDBStatement = "db.statement";
        public const string AttributeDBOperation = "db.operation";
        public const string AttributeDBInstance = "db.instance";
        public const string AttributeDBURL = "db.url";
        public const string AttributeDBCassandraKeyspace = "db.cassandra.keyspace";
        public const string AttributeDBHBaseNamespace = "db.hbase.namespace";
        public const string AttributeDBRedisDatabaseIndex = "db.redis.database_index";
        public const string AttributeDBMongoDBCollection = "db.mongodb.collection";

        public const string AttributeRPCSystem = "rpc.system";
        public const string AttributeRPCService = "rpc.service";
        public const string AttributeRPCMethod = "rpc.method";

        public const string AttributeMessageType = "message.type";
        public const string AttributeMessageID = "message.id";
        public const string AttributeMessageCompressedSize = "message.compressed_size";
        public const string AttributeMessageUncompressedSize = "message.uncompressed_size";

        public const string AttributeFaaSTrigger = "faas.trigger";
        public const string AttributeFaaSExecution = "faas.execution";
        public const string AttributeFaaSDocumentCollection = "faas.document.collection";
        public const string AttributeFaaSDocumentOperation = "faas.document.operation";
        public const string AttributeFaaSDocumentTime = "faas.document.time";
        public const string AttributeFaaSDocumentName = "faas.document.name";
        public const string AttributeFaaSTime = "faas.time";
        public const string AttributeFaaSCron = "faas.cron";

        public const string AttributeMessagingSystem = "messaging.system";
        public const string AttributeMessagingDestination = "messaging.destination";
        public const string AttributeMessagingDestinationKind = "messaging.destination_kind";
        public const string AttributeMessagingTempDestination = "messaging.temp_destination";
        public const string AttributeMessagingProtocol = "messaging.protocol";
        public const string AttributeMessagingProtocolVersion = "messaging.protocol_version";
        public const string AttributeMessagingURL = "messaging.url";
        public const string AttributeMessagingMessageID = "messaging.message_id";
        public const string AttributeMessagingConversationID = "messaging.conversation_id";
        public const string AttributeMessagingPayloadSize = "messaging.message_payload_size_bytes";
        public const string AttributeMessagingPayloadCompressedSize = "messaging.message_payload_compressed_size_bytes";
        public const string AttributeMessagingOperation = "messaging.operation";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
