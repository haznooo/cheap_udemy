using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using Supabase;

namespace Business.Services
{

    // latter when i start having proper DI i will move this to somewhere else
    public interface IMediaService
    {
        Task<string> UploadFileAsync(IFormFile file);
    }

    public class SupabaseMediaService : IMediaService
    {
        private readonly Client _supabaseClient;
        private const string BucketName = "course-media"; // Must match what you named it in supabase

        public SupabaseMediaService(Client supabaseClient)
        {
            _supabaseClient = supabaseClient;
        }
        public async Task<string> UploadFileAsync(IFormFile file)
        {
            // 1. Read the file into a byte array
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            // 2. Generate a unique file name so we don't overwrite existing files
            var extension = Path.GetExtension(file.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";

      
            // 1. Upload to the private bucket 
            await _supabaseClient.Storage
                .From(BucketName)
                .Upload(fileBytes, uniqueFileName, new Supabase.Storage.FileOptions { Upsert = false });

            // 2. Instead of returning a public URL, return ONLY the unique file name!
            // This is what gets saved to your lesson content blocks JSON.
            return uniqueFileName;
        }
    }
}


