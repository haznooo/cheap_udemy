namespace Business.Dto.Rsponse
{
    // Typed upload responses so the OpenAPI spec (and the client generated from it)
    // sees the real wire shapes instead of untyped anonymous objects. Serialized
    // with the default camelCase policy, so the JSON matches the old anonymous
    // shapes exactly ("thumbnail" / "url" / "avatar").
    public record ThumbnailUploadResponse(string Thumbnail);
    public record MediaUploadResponse(string Url);
    public record AvatarUploadResponse(string Avatar);
}
