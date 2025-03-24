using System.Collections.Generic;

namespace SupportBot.Core.Interfaces.Services
{
    public interface ICompanyService
    {
        IEnumerable<string> GetAllCompanies();
        IEnumerable<string> GetCompaniesByLetter(char letter);
        void AddCompanyIfNotExists(string companyName);
    }
}
