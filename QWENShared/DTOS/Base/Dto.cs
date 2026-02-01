namespace QWENShared.DTOS.Base
{
    public interface IValidationRequest
    {
        string ValidationKey { get; set; } // Ej: "Name", "PhoneMobile"
    }
    public interface IDto
    {
        Guid Id { get; set; }
        bool IsCreated { get; }
        int Order { get; set; }
    }

    public class Dto : IDto, IValidationRequest
    {
        public Guid Id { get; set; } = Guid.Empty;
        public bool IsCreated => Id != Guid.Empty;
        public int Order { get; set; }
        public string ValidationKey { get; set; } = string.Empty;

    }

}
