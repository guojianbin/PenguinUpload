﻿using Nancy;
using Nancy.Responses;
using Nancy.Security;
using PenguinUpload.Infrastructure.Upload;
using PenguinUpload.Services.Authentication;
using PenguinUpload.Services.FileStorage;
using PenguinUpload.Utilities;

namespace PenguinUpload.Modules
{
    public class AdminAccessModule : NancyModule
    {
        public IPenguinUploadContext ServerContext;

        public AdminAccessModule(IPenguinUploadContext serverContext) : base("/api/admin")
        {
            ServerContext = serverContext;

            this.RequiresAuthentication();
            // Requires API key access
            this.RequiresClaims(x => x.Value == ApiClientAuthenticationService.StatelessAuthClaim.Value);

            Before += (ctx) =>
            {
                // Make sure user is an admin
                if (!ServerContext.IsAdministrator(Context.CurrentUser.Identity.Name))
                {
                    return HttpStatusCode.Unauthorized;
                }
                return null;
            };

            // List all users
            Get("/enumerateusers", async _ =>
            {
                var webUserManager = new WebUserManager(ServerContext);
                var allUsers = await webUserManager.GetAllUsersAsync();
                return Response.AsJsonNet(allUsers);
            });

            // Get user account info
            Get("/accountinfo/{name}", async args =>
            {
                var userManager = new WebUserManager(ServerContext);
                var user = await userManager.FindUserByUsernameAsync((string)args.name);
                return user == null ? HttpStatusCode.NotFound : Response.AsJsonNet(user);
            });

            // Disable a user's account
            Patch("/disableuser/{name}", async args =>
            {
                var userManager = new WebUserManager(ServerContext);
                var user = await userManager.FindUserByUsernameAsync((string)args.name);
                if (user == null) return HttpStatusCode.BadRequest;
                // Disable user
                await userManager.SetEnabledAsync(user, false);
                return HttpStatusCode.OK;
            });

            // Enable a user's account
            Patch("/enableuser/{name}", async args =>
            {
                var userManager = new WebUserManager(ServerContext);
                var user = await userManager.FindUserByUsernameAsync((string)args.name);
                if (user == null) return HttpStatusCode.BadRequest;
                // Disable user
                await userManager.SetEnabledAsync(user, true);
                return HttpStatusCode.OK;
            });

            // Get file info (admin override)
            Get("/fileinfo/{id}", async args =>
            {
                var fileId = (string)args.id;
                // Get metadata
                var storedFilesManager = new StoredFilesManager(ServerContext);
                var storedFile = await storedFilesManager.GetStoredFileByIdentifierAsync(fileId);
                return Response.AsJsonNet(storedFile);
            });

            // Download a file (admin override)
            Get("/downloadfile/{id}", async args =>
            {
                var fileId = (string)args.id;
                // Get metadata
                var storedFilesManager = new StoredFilesManager(ServerContext);
                var storedFile = await storedFilesManager.GetStoredFileByIdentifierAsync(fileId);
                if (storedFile == null) return HttpStatusCode.NotFound;
                var fileUploadHandler = new LocalStorageHandler(ServerContext, null, true);
                var fileStream = fileUploadHandler.RetrieveFileStream(storedFile.Identifier);
                var response = new StreamResponse(() => fileStream, MimeTypes.GetMimeType(storedFile.Name));
                return response.AsAttachment(storedFile.Name);
            });

            // Delete a file (admin override)
            Delete("/deletefile/{id}", async args =>
            {
                var fileId = (string)args.id;
                // Remove physical file
                var fileUploadHandler = new LocalStorageHandler(ServerContext, null, true);
                await fileUploadHandler.DeleteFileAsync(fileId);
                // Unregister file
                var storedFilesManager = new StoredFilesManager(ServerContext);
                await storedFilesManager.UnregisterStoredFileAsync(fileId);
                return HttpStatusCode.OK;
            });

            // Quota management
            Patch("/updatequota/{name}/{quota}", async args =>
            {
                var userManager = new WebUserManager(ServerContext);
                var user = await userManager.FindUserByUsernameAsync((string)args.name);
                if (user == null) return HttpStatusCode.BadRequest;
                // Disable user
                await userManager.SetQuotaAsync(user, (int)args.quota);
                return HttpStatusCode.OK;
            });
        }
    }
}