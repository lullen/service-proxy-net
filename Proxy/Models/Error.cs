using System.Text.Json.Serialization;

namespace Proxy.Models
{
    public class Error
    {
        public ErrorCode Code { get; set; } = ErrorCode.None;
        public string Description { get; set; } = string.Empty;

        public Error(ErrorCode code, string description)
        {
            Code = code;
            Description = description;
        }

        public Error(Error error)
        {
            Code = error.Code;
            Description = error.Description;
        }

        public Error() { }
        public static Error Empty
        {
            get
            {
                return new Error();
            }
        }

        [JsonIgnore]
        public bool HasError { get { return Code != ErrorCode.None; } }
    }
}
