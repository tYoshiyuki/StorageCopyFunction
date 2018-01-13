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
            log.Info($"�����J�n {DateTime.Now}");

            // �X�g���[�W�A�J�E���g�̐���
            var storageAccountSrc = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["StorageConnectionStringSrc"].ConnectionString);
            var storageAccountDest = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["StorageConnectionStringDest"].ConnectionString);

            // �t�@�C���X�g���[�W�N���C�A���g�𐶐�
            var fileClientSrc = storageAccountSrc.CreateCloudFileClient();
            var fileClientDest = storageAccountDest.CreateCloudFileClient();

            // ���L�t�H���_�̎Q�Ƃ𐶐�
            var fileShareName = "test";
            var shareSrc = fileClientSrc.GetShareReference(fileShareName);
            var shareDest = fileClientDest.GetShareReference(fileShareName);

            if (shareSrc.Exists() && shareDest.Exists())
            {
                // ���[�g�f�B���N�g���̎Q�Ƃ𐶐�
                var rootDirSrc = shareSrc.GetRootDirectoryReference();
                var rootDirDest = shareDest.GetRootDirectoryReference();

                // �R�s�[���f�B���N�g���̑S�t�@�C���ɑ΂��ď������s��
                rootDirSrc.ListFilesAndDirectories().Where(_ => _.GetType() == typeof(CloudFile))
                    .ToList().ForEach(_ =>
                {
                    var name = Path.GetFileName(_.Uri.LocalPath);
                    var fileSrc = rootDirSrc.GetFileReference(name);

                    // ���[�W�����Ԃ̃R�s�[�ł�SAS���K�v�Ȃ̂Ő�������
                    var fileSas = fileSrc.GetSharedAccessSignature(new SharedAccessFilePolicy()
                    {
                        Permissions = SharedAccessFilePermissions.Read,
                        SharedAccessExpiryTime = DateTime.UtcNow.AddHours(24)
                    });

                    var fileSasUri = new Uri(fileSrc.StorageUri.PrimaryUri.ToString() + fileSas);
                    var fileDest = rootDirDest.GetFileReference(name);
                
                    fileDest.StartCopy(fileSasUri); // �R�s�[���������{����
                    // ���[�W�����Ԃ̃R�s�[�͔񓯊��Ɏ��{����邽�߃R�s�[�󋵂̃X�e�[�^�X���`�F�b�N����
                    while (fileDest.CopyState.Status == CopyStatus.Pending)
                    {
                        Task.Delay(500);
                        fileDest.FetchAttributes();
                    }

                    fileSrc.Delete(); // �R�s�[���t�@�C�����폜����
                    log.Info($"�t�@�C���R�s�[ {name}");
                });
            }
            log.Info($"�������� {DateTime.Now}");
        }
    }
}
