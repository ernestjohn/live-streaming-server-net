﻿using Azure.Storage.Blobs;
using LiveStreamingServerNet.StreamProcessor.AzureBlobStorage.Configurations;
using LiveStreamingServerNet.StreamProcessor.AzureBlobStorage.Internal.Logging;
using LiveStreamingServerNet.StreamProcessor.Hls;
using LiveStreamingServerNet.StreamProcessor.Hls.Contracts;
using Microsoft.Extensions.Logging;

namespace LiveStreamingServerNet.StreamProcessor.AzureBlobStorage.Internal
{
    internal class HlsAzureBlobStorageAdapter : IHlsStorageAdapter
    {
        private readonly BlobContainerClient _containerClient;
        private readonly HlsAzureBlobStorageConfiguration _config;
        private readonly ILogger _logger;

        public HlsAzureBlobStorageAdapter(
            BlobContainerClient containerClient,
            HlsAzureBlobStorageConfiguration config,
            ILogger<HlsAzureBlobStorageAdapter> logger)
        {
            _containerClient = containerClient;
            _config = config;
            _logger = logger;
        }

        public async Task<StoringResult> StoreAsync(
            StreamProcessingContext context,
            IReadOnlyList<Manifest> manifests,
            IReadOnlyList<TsFile> tsFiles,
            CancellationToken cancellationToken)
        {
            var storedTsFiles = await UploadTsFilesAsync(context, tsFiles, cancellationToken);
            var storedManifestFiles = await UploadManifestFilesAsync(context, manifests, cancellationToken);
            return new StoringResult(storedManifestFiles, storedTsFiles);
        }

        private async Task<IReadOnlyList<StoredTsFile>> UploadTsFilesAsync(
            StreamProcessingContext context,
            IReadOnlyList<TsFile> tsFiles,
            CancellationToken cancellationToken)
        {
            var dirPath = Path.GetDirectoryName(context.OutputPath) ?? string.Empty;

            var tasks = new List<Task<StoredTsFile>>();

            foreach (var tsFile in tsFiles)
            {
                var tsFilePath = Path.Combine(dirPath, tsFile.FileName);
                tasks.Add(UploadTsFileAsync(tsFile.FileName, tsFilePath, cancellationToken));
            }

            return await Task.WhenAll(tasks);

            async Task<StoredTsFile> UploadTsFileAsync
                (string tsFileName, string tsFilePath, CancellationToken cancellationToken)
            {
                try
                {
                    var blobPath = _config.BlobPathResolver.ResolveBlobPath(context, tsFileName);
                    var blobClient = _containerClient.GetBlobClient(blobPath);

                    var response = await blobClient.UploadAsync(tsFilePath, _config.TsFilesUploadOptions, cancellationToken);
                    return new StoredTsFile(tsFileName, blobClient.Uri);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.UploadingTsFileError(
                        context.Processor, context.Identifier, context.InputPath, context.OutputPath, context.StreamPath, tsFilePath, ex);

                    return new StoredTsFile(tsFileName, null);
                }
            }
        }

        private async Task<IReadOnlyList<StoredManifest>> UploadManifestFilesAsync(
           StreamProcessingContext context,
           IReadOnlyList<Manifest> manifests,
           CancellationToken cancellationToken)
        {
            var tasks = new List<Task<StoredManifest>>();

            foreach (var manifest in manifests)
            {
                tasks.Add(UploadManifestAsync(manifest.Name, manifest.Content, cancellationToken));
            }

            return await Task.WhenAll(tasks);

            async Task<StoredManifest> UploadManifestAsync
                (string name, string content, CancellationToken cancellationToken)
            {
                try
                {
                    var blobPath = _config.BlobPathResolver.ResolveBlobPath(context, name);
                    var blobClient = _containerClient.GetBlobClient(blobPath);

                    await blobClient.UploadAsync(new BinaryData(content), _config.ManifestsUploadOptions, cancellationToken);
                    return new StoredManifest(name, blobClient.Uri);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.UploadingManifestFileError(
                        context.Processor, context.Identifier, context.InputPath, context.OutputPath, context.StreamPath, name, ex);

                    return new StoredManifest(name, null);
                }
            }
        }

        public async Task DeleteAsync(StreamProcessingContext context, IReadOnlyList<TsFile> tsFiles, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            foreach (var tsFile in tsFiles)
            {
                tasks.Add(DeleteTsFileAsync(tsFile.FileName, cancellationToken));
            }

            await Task.WhenAll(tasks);

            async Task DeleteTsFileAsync
                (string tsFileName, CancellationToken cancellationToken)
            {
                try
                {
                    var blobPath = _config.BlobPathResolver.ResolveBlobPath(context, tsFileName);
                    var blobClient = _containerClient.GetBlobClient(blobPath);

                    await blobClient.DeleteAsync(cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.DeletingTsFileError(
                        context.Processor, context.Identifier, context.InputPath, context.OutputPath, context.StreamPath, ex);
                }
            }
        }
    }
}
