using System.Net.Http.Headers;
using System.Text;
using VkNet;
using VkNet.Enums.Filters;
using VkNet.Model;
using WildBerriesAnalyzer.Business.Properties;

namespace WildBerriesAnalyzer.Business.VK
{
    public class VkPublisher
    {
        private VkApi _vk;

        public VkPublisher()
        {
            _vk = new VkApi();
        }

        public bool Authorize(string twoFactor, ulong? sid = null, string? key = null)
        {
            try
            {
                _vk.Authorize(new ApiAuthParams
                {
                    ApplicationId = ulong.Parse(Resources.AppId),
                    Login = Resources.VkLogin,
                    Password = Resources.VkPassword,
                    TwoFactorSupported = true,
                    Settings = Settings.All,
                    CaptchaSid = sid,
                    CaptchaKey = key,
                    TwoFactorAuthorization = () =>
                    {
                        return twoFactor;
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> CreatePost(string message, string? excelFile)
        {
            List<MediaAttachment>? attachments = null;

            var server = await _vk.Docs.GetWallUploadServerAsync(long.Parse(Resources.GroupId));

            var response = await UploadFile(server.UploadUrl, excelFile, Path.GetExtension(excelFile));

            string title = Path.GetFileName(excelFile);

            try
            {
                attachments = new List<MediaAttachment>
                    {
                        _vk.Docs.Save(response, title ?? Guid.NewGuid().ToString(), null)
                                .First()
                                .Instance
                    };

                await _vk.Wall.PostAsync(new WallPostParams
                {
                    OwnerId = long.Parse(Resources.WallGroupId),
                    Attachments = attachments,
                    FromGroup = true,
                    Message = message
                });

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task<string> UploadFile(string serverUrl, string file, string fileExtension)
        {
            // Получение массива байтов из файла
            var data = File.ReadAllBytes(file);

            // Создание запроса на загрузку файла на сервер
            using (var client = new HttpClient())
            {
                var requestContent = new MultipartFormDataContent();
                var content = new ByteArrayContent(data);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
                requestContent.Add(content, "file", $"file.{fileExtension}");

                var response = client.PostAsync(serverUrl, requestContent).Result;
                return Encoding.Default.GetString(await response.Content.ReadAsByteArrayAsync());
            }
        }


    }
}
