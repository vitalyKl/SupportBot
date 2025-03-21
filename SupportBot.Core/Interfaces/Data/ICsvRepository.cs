using System.Collections.Generic;
using System.Threading.Tasks;

namespace SupportBot.Core.Interfaces.Data
{
    // Обобщённый интерфейс для работы с CSV (импровизированная БД)
    public interface ICsvRepository<T>
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task SaveAsync(T item);
    }
}
