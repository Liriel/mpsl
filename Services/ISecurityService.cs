using System.Threading.Tasks;

namespace mps.Services{

    public interface ISecurityService{
        
        Task ClaimAdminAsync(string userId);

        Task PromoteAsync(string userId);
    
    }
}