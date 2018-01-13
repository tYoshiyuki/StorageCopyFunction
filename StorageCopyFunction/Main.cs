using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.File;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StorageCopyFunction
{
    public static class Main
    {
        [FunctionName("Main")]
        public static void Run([TimerTrigger("*/10 * * * * *")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info($"処理開始 {DateTime.Now}");

            // ストレージアカウントの生成
            var storageAccountSrc = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["StorageConnectionStringSrc"].ConnectionString);
            var storageAccountDest = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["StorageConnectionStringDest"].ConnectionString);

            // ファイルストレージクライアントを生成
            var fileClientSrc = storageAccountSrc.CreateCloudFileClient();
            var fileClientDest = storageAccountDest.CreateCloudFileClient();

            // 共有フォルダの参照を生成
            var fileShareName = "test";
            var shareSrc = fileClientSrc.GetShareReference(fileShareName);
            var shareDest = fileClientDest.GetShareReference(fileShareName);

            if (shareSrc.Exists() && shareDest.Exists())
            {
                // ルートディレクトリの参照を生成
                var rootDirSrc = shareSrc.GetRootDirectoryReference();
                var rootDirDest = shareDest.GetRootDirectoryReference();

                // コピー元ディレクトリの全ファイルに対して処理を行う
                rootDirSrc.ListFilesAndDirectories().Where(_ => _.GetType() == typeof(CloudFile))
                    .ToList().ForEach(_ =>
                {
                    var name = Path.GetFileName(_.Uri.LocalPath);
                    var fileSrc = rootDirSrc.GetFileReference(name);

                    // リージョン間のコピーではSASが必要なので生成する
                    var fileSas = fileSrc.GetSharedAccessSignature(new SharedAccessFilePolicy()
                    {
                        Permissions = SharedAccessFilePermissions.Read,
                        SharedAccessExpiryTime = DateTime.UtcNow.AddHours(24)
                    });

                    var fileSasUri = new Uri(fileSrc.StorageUri.PrimaryUri.ToString() + fileSas);
                    var fileDest = rootDirDest.GetFileReference(name);
                
                    fileDest.StartCopy(fileSasUri); // コピー処理を実施する
                    // リージョン間のコピーは非同期に実施されるためコピー状況のステータスをチェックする
                    while (fileDest.CopyState.Status == CopyStatus.Pending)
                    {
                        Task.Delay(500);
                        fileDest.FetchAttributes();
                    }

                    fileSrc.Delete(); // コピー元ファイルを削除する
                    log.Info($"ファイルコピー {name}");
                });
            }
            log.Info($"処理完了 {DateTime.Now}");
        }
    }
}
