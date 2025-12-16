public interface IResourceSave
{
    public Task<bool> RemoveList(List<ImageShare> uris);
    public Task<bool> RemoveSingle(ImageShare resource);
    public Task<byte[]> GetResource(ImageShare resource);
    public Task<string> SaveResource(Guid resourceId, byte[] img, string? fileType, int maxScreenSize, ImageShareSource source);

}