namespace SupportBot.Core.Interfaces.Services
{
    public interface ICompanyBindingService
    {
        string? GetBinding(string authorId);
        void SaveBinding(string authorId, string companyName);
    }
}
