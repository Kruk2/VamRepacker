using System.Runtime.Serialization;

namespace VamToolbox;

[Serializable]
internal class VamToolboxException : Exception
{
    public VamToolboxException()
    {
    }

    public VamToolboxException(string message)
        : base(message)
    {
    }

    public VamToolboxException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    protected VamToolboxException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}