using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Oqtane.Documentation;
using Oqtane.Models;
using Oqtane.Shared;
using Oqtane.UI;

namespace Oqtane.Services
{
    [PrivateApi("Don't show in the documentation, as everything should use the Interface")]
    public class FileService : ServiceBase, IFileService
    {
        private readonly SiteState _siteState;
        private readonly IJSRuntime _jsRuntime;

        public FileService(HttpClient http, SiteState siteState, IJSRuntime jsRuntime) : base(http, siteState)
        {
            _siteState = siteState;
            _jsRuntime = jsRuntime;
        }

        private string Apiurl => CreateApiUrl("File");

        public async Task<List<File>> GetFilesAsync(int folderId)
        {
            return await GetFilesAsync(folderId.ToString());
        }

        public async Task<List<File>> GetFilesAsync(string folder)
        {
            List<File> files = await GetJsonAsync<List<File>>($"{Apiurl}?folder={folder}");
            return files.OrderBy(item => item.Name).ToList();
        }

        public async Task<List<File>> GetFilesAsync(int siteId, string folderPath)
        {
            if (!(folderPath.EndsWith(System.IO.Path.DirectorySeparatorChar) || folderPath.EndsWith(System.IO.Path.AltDirectorySeparatorChar)))
            {
                folderPath = Utilities.PathCombine(folderPath, System.IO.Path.DirectorySeparatorChar.ToString());
            }

            var path = WebUtility.UrlEncode(folderPath);

            List<File> files = await GetJsonAsync<List<File>>($"{Apiurl}/{siteId}/{path}");
            return files?.OrderBy(item => item.Name).ToList();
        }

        public async Task<File> GetFileAsync(int fileId)
        {
            return await GetJsonAsync<File>($"{Apiurl}/{fileId}");
        }

        public async Task<File> AddFileAsync(File file)
        {
            return await PostJsonAsync<File>(Apiurl, file);
        }

        public async Task<File> UpdateFileAsync(File file)
        {
            return await PutJsonAsync<File>($"{Apiurl}/{file.FileId}", file);
        }

        public async Task DeleteFileAsync(int fileId)
        {
            await DeleteAsync($"{Apiurl}/{fileId}");
        }

        public async Task<File> UploadFileAsync(string url, int folderId, string name)
        {
            return await GetJsonAsync<File>($"{Apiurl}/upload?url={WebUtility.UrlEncode(url)}&folderid={folderId}&name={name}");
        }

        public async Task<string> UploadFilesAsync(int folderId, string[] files, string id)
        {
            return await UploadFilesAsync(folderId.ToString(), files, id);
        }

        public async Task<string> UploadFilesAsync(string folder, string[] files, string id)
        {
            string result = "";

            var interop = new Interop(_jsRuntime);
            await interop.UploadFiles($"{Apiurl}/upload", folder, id, _siteState.AntiForgeryToken);

            // uploading files is asynchronous so we need to wait for the upload to complete
            bool success = false;
            int attempts = 0;
            while (attempts < 5 && success == false)
            {
                Thread.Sleep(2000); // wait 2 seconds
                result = "";

                List<File> fileList = await GetFilesAsync(folder);
                if (fileList.Count > 0)
                {
                    success = true;
                    foreach (string file in files)
                    {
                        if (!fileList.Exists(item => item.Name == file))
                        {
                            success = false;
                            result += file + ",";
                        }
                    }
                }

                attempts += 1;
            }

            await interop.SetElementAttribute(id + "ProgressInfo", "style", "display: none;");
            await interop.SetElementAttribute(id + "ProgressBar", "style", "display: none;");

            if (!success)
            {
                result = result.Substring(0, result.Length - 1);
            }

            return result;
        }

        public async Task<byte[]> DownloadFileAsync(int fileId)
        {
            return await GetByteArrayAsync($"{Apiurl}/download/{fileId}");
        }
    }
}
