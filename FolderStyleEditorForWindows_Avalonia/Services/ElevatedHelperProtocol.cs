using Newtonsoft.Json;

namespace FolderStyleEditorForWindows.Services
{
    public enum ElevatedHelperCommand
    {
        Handshake,
        SaveFolderStyle,
        Ping,
        Shutdown
    }

    public sealed class ElevatedHelperRequest
    {
        [JsonProperty("command")]
        public ElevatedHelperCommand Command { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;

        [JsonProperty("protocolVersion")]
        public int ProtocolVersion { get; set; } = 1;

        [JsonProperty("payload")]
        public FolderStyleMutationRequest? Payload { get; set; }
    }

    public sealed class ElevatedHelperResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("result")]
        public FolderStyleMutationResult? Result { get; set; }
    }
}
