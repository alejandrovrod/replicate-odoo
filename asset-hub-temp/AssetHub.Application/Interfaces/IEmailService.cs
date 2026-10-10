using System.Threading;
using System.Threading.Tasks;

namespace AssetHub.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = false, CancellationToken cancellationToken = default);
}
