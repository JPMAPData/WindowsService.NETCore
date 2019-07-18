using Autodesk.Forge;
using Autodesk.Forge.Client;
using Autodesk.Forge.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace CoreserviceTest
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var service = new TestService())
            {
                ServiceBase.Run(service);
            }
        }
        internal class TestService : ServiceBase
        {
            public static string token;

            public TestService()
            {
                ServiceName = "TestService";
            }

            protected override void OnStart(string[] args)
            {
                string clientID = "Dm1AJ95famLKnf4MUOGpwO7zJIcBF4J7";
                string clientSecret = "vAyaauIpMr4qhy6O";

                TwoLeggedApi _twoLeggedApi = new TwoLeggedApi();

                Task<ApiResponse<dynamic>> retorno = _twoLeggedApi.AuthenticateAsyncWithHttpInfo(clientID, clientSecret, oAuthConstants.CLIENT_CREDENTIALS, new Scope[] { Scope.DataRead, Scope.DataCreate, Scope.DataWrite, Scope.ViewablesRead });

                if (retorno.Result.StatusCode < 200 || retorno.Result.StatusCode >= 300)
                {
                    throw new Exception("Erro de CONEXAO");
                }

                token = retorno.Result.Data.access_token;

                UploadAsync().GetAwaiter().GetResult();

                string filename = CheckFileExists();
                File.AppendAllText(filename, $"{DateTime.Now} started.{Environment.NewLine}");

            }

            public static async Task UploadAsync()
            {


                ProjectsApi projectsApi = new ProjectsApi();

                StorageRelationshipsTargetData storageRelData = new StorageRelationshipsTargetData(StorageRelationshipsTargetData.TypeEnum.Folders, "urn:adsk.wipprod:fs.folder:co.OoAFTIjQTdCEF2HJUjee0g");
                CreateStorageDataRelationshipsTarget storageTarget = new CreateStorageDataRelationshipsTarget(storageRelData);
                CreateStorageDataRelationships storageRel = new CreateStorageDataRelationships(storageTarget);
                BaseAttributesExtensionObject attributes = new BaseAttributesExtensionObject(string.Empty, string.Empty, new JsonApiLink(string.Empty), null);
                CreateStorageDataAttributes storageAtt = new CreateStorageDataAttributes("test", attributes);
                CreateStorageData storageData = new CreateStorageData(CreateStorageData.TypeEnum.Objects, storageAtt, storageRel);
                CreateStorage storage = new CreateStorage(new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0), storageData);
                dynamic storageCreated = await projectsApi.PostStorageAsync("3b5c6352-cf34-41fd-a0e9-a5246d93bf55", storage);

                string[] storageIdParams = ((string)storageCreated.data.id).Split('/');
                string[] bucketKeyParams = storageIdParams[storageIdParams.Length - 2].Split(':');
                string bucketKey = bucketKeyParams[bucketKeyParams.Length - 1];
                string objectName = storageIdParams[storageIdParams.Length - 1];

                ObjectsApi objects = new ObjectsApi();
                objects.Configuration.AccessToken = token;

                long fileSize = (new FileInfo(@"C:\Users\joao.martins\Desktop\ESGOTO_R2.rvt")).Length;

                if (fileSize > 5 * 1024 * 1024) // upload in chunks if > 5Mb
                {
                    long chunkSize = 2 * 1024 * 1024; // 2 Mb
                    long numberOfChunks = (long)Math.Round((double)(fileSize / chunkSize)) + 1;

                    long start = 0;
                    chunkSize = (numberOfChunks > 1 ? chunkSize : fileSize);
                    long end = chunkSize;
                    string sessionId = Guid.NewGuid().ToString();

                    // upload one chunk at a time
                    using (BinaryReader reader = new BinaryReader(new FileStream(@"C:\Users\joao.martins\Desktop\ESGOTO_R2.rvt", FileMode.Open)))
                    {
                        for (int chunkIndex = 0; chunkIndex < numberOfChunks; chunkIndex++)
                        {
                            string range = string.Format("bytes {0}-{1}/{2}", start, end, fileSize);

                            long numberOfBytes = chunkSize + 1;
                            byte[] fileBytes = new byte[numberOfBytes];
                            MemoryStream memoryStream = new MemoryStream(fileBytes);
                            reader.BaseStream.Seek((int)start, SeekOrigin.Begin);
                            int count = reader.Read(fileBytes, 0, (int)numberOfBytes);
                            memoryStream.Write(fileBytes, 0, (int)numberOfBytes);
                            memoryStream.Position = 0;

                            await objects.UploadChunkAsync(bucketKey, objectName, (int)numberOfBytes, range, sessionId, memoryStream);

                            start = end + 1;
                            chunkSize = ((start + chunkSize > fileSize) ? fileSize - start - 1 : chunkSize);
                            end = start + chunkSize;
                        }
                    }
                }
                else // upload in a single call
                {
                    using (StreamReader streamReader = new StreamReader(@"C:\Users\joao.martins\Desktop\ESGOTO_R2.rvt"))
                    {
                        await objects.UploadObjectAsync(bucketKey, objectName, (int)streamReader.BaseStream.Length, streamReader.BaseStream, "application/octet-stream");
                    }
                }

                // check if file already exists...
                FoldersApi folderApi = new FoldersApi();
                folderApi.Configuration.AccessToken = token;
                var filesInFolder = await folderApi.GetFolderContentsAsync("3b5c6352-cf34-41fd-a0e9-a5246d93bf55", "urn:adsk.wipprod:fs.folder:co.OoAFTIjQTdCEF2HJUjee0g");
                string itemId = string.Empty;
                foreach (KeyValuePair<string, dynamic> item in new DynamicDictionaryItems(filesInFolder.data))
                    if (item.Value.attributes.displayName == "test")
                        itemId = item.Value.id; // this means a file with same name is already there, so we'll create a new version

                // now decide whether create a new item or new version
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    // create a new item
                    BaseAttributesExtensionObject baseAttribute = new BaseAttributesExtensionObject("3b5c6352-cf34-41fd-a0e9-a5246d93bf55".StartsWith("a.") ? "items:autodesk.core:File" : "items:autodesk.bim360:File", "1.0");
                    CreateItemDataAttributes createItemAttributes = new CreateItemDataAttributes("test", baseAttribute);
                    CreateItemDataRelationshipsTipData createItemRelationshipsTipData = new CreateItemDataRelationshipsTipData(CreateItemDataRelationshipsTipData.TypeEnum.Versions, CreateItemDataRelationshipsTipData.IdEnum._1);
                    CreateItemDataRelationshipsTip createItemRelationshipsTip = new CreateItemDataRelationshipsTip(createItemRelationshipsTipData);
                    StorageRelationshipsTargetData storageTargetData = new StorageRelationshipsTargetData(StorageRelationshipsTargetData.TypeEnum.Folders, "urn:adsk.wipprod:fs.folder:co.OoAFTIjQTdCEF2HJUjee0g");
                    CreateStorageDataRelationshipsTarget createStorageRelationshipTarget = new CreateStorageDataRelationshipsTarget(storageTargetData);
                    CreateItemDataRelationships createItemDataRelationhips = new CreateItemDataRelationships(createItemRelationshipsTip, createStorageRelationshipTarget);
                    CreateItemData createItemData = new CreateItemData(CreateItemData.TypeEnum.Items, createItemAttributes, createItemDataRelationhips);
                    BaseAttributesExtensionObject baseAttExtensionObj = new BaseAttributesExtensionObject("3b5c6352-cf34-41fd-a0e9-a5246d93bf55".StartsWith("a.") ? "versions:autodesk.core:File" : "versions:autodesk.bim360:File", "1.0");
                    CreateStorageDataAttributes storageDataAtt = new CreateStorageDataAttributes("test", baseAttExtensionObj);
                    CreateItemRelationshipsStorageData createItemRelationshipsStorageData = new CreateItemRelationshipsStorageData(CreateItemRelationshipsStorageData.TypeEnum.Objects, storageCreated.data.id);
                    CreateItemRelationshipsStorage createItemRelationshipsStorage = new CreateItemRelationshipsStorage(createItemRelationshipsStorageData);
                    CreateItemRelationships createItemRelationship = new CreateItemRelationships(createItemRelationshipsStorage);
                    CreateItemIncluded includedVersion = new CreateItemIncluded(CreateItemIncluded.TypeEnum.Versions, CreateItemIncluded.IdEnum._1, storageDataAtt, createItemRelationship);
                    CreateItem createItem = new CreateItem(new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0), createItemData, new List<CreateItemIncluded>() { includedVersion });

                    ItemsApi itemsApi = new ItemsApi();
                    itemsApi.Configuration.AccessToken = token;
                    var newItem = await itemsApi.PostItemAsync("3b5c6352-cf34-41fd-a0e9-a5246d93bf55", createItem);
                }
                else
                {
                    // create a new version
                    BaseAttributesExtensionObject attExtensionObj = new BaseAttributesExtensionObject("3b5c6352-cf34-41fd-a0e9-a5246d93bf55".StartsWith("a.") ? "versions:autodesk.core:File" : "versions:autodesk.bim360:File", "1.0");
                    CreateStorageDataAttributes storageDataAtt = new CreateStorageDataAttributes("test", attExtensionObj);
                    CreateVersionDataRelationshipsItemData dataRelationshipsItemData = new CreateVersionDataRelationshipsItemData(CreateVersionDataRelationshipsItemData.TypeEnum.Items, itemId);
                    CreateVersionDataRelationshipsItem dataRelationshipsItem = new CreateVersionDataRelationshipsItem(dataRelationshipsItemData);
                    CreateItemRelationshipsStorageData itemRelationshipsStorageData = new CreateItemRelationshipsStorageData(CreateItemRelationshipsStorageData.TypeEnum.Objects, storageCreated.data.id);
                    CreateItemRelationshipsStorage itemRelationshipsStorage = new CreateItemRelationshipsStorage(itemRelationshipsStorageData);
                    CreateVersionDataRelationships dataRelationships = new CreateVersionDataRelationships(dataRelationshipsItem, itemRelationshipsStorage);
                    CreateVersionData versionData = new CreateVersionData(CreateVersionData.TypeEnum.Versions, storageDataAtt, dataRelationships);
                    CreateVersion newVersionData = new CreateVersion(new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0), versionData);

                    VersionsApi versionsApis = new VersionsApi();
                    versionsApis.Configuration.AccessToken = token;
                    dynamic newVersion = await versionsApis.PostVersionAsync("3b5c6352-cf34-41fd-a0e9-a5246d93bf55", newVersionData);
                }
            }

            protected override void OnStop()
            {
                string filename = CheckFileExists();
                File.AppendAllText(filename, $"{DateTime.Now} stopped.{Environment.NewLine}");
            }

            private static string CheckFileExists()
            {
                string filename = @"C:\Users\joao.martins\Desktop\serv.txt";
                if (!File.Exists(filename))
                {
                    File.Create(filename);
                }
                
                return filename;
            }
        }
    }
}
