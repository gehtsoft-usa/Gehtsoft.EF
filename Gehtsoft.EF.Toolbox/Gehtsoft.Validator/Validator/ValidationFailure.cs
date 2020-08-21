using System.Runtime.CompilerServices;

namespace Gehtsoft.Validator
{
    public class ValidationFailure
    {
        public string Name { get; internal set; }
        public int Code { get; set; }
        public string Path { get; internal set; }
        public string Message { get; internal set; }

        public override string ToString() => $"[{Path}] {Code} : {Message ?? "null"}";

        internal ValidationFailure()
        {

        }

        public ValidationFailure(string name, string path, int code, string message)
        {
            Name = name;
            Path = path;
            Code = code;
            Message = message;
        }

        public ValidationFailure(string name, int code, string message)
        {
            Name = name;
            Path = name;
            Code = code;
            Message = message;
        }

        public ValidationFailure(string name, int code)
        {
            Name = name;
            Path = name;
            Code = code;
            Message = null;
        }

        public ValidationFailure(string name, string message)
        {
            Name = name;
            Path = name;
            Code = 0;
            Message = message;
        }
    }
}
