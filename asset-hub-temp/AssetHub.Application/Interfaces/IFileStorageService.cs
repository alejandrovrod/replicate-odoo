using System.IO;
using System.Threading.Tasks;

namespace AssetHub.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(Stream content, string fileName, string contentType);
}
