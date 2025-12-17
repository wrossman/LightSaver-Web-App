public interface IResourceSave
{
    public Task<bool> RemoveList(List<ImageShare> uris);
    public Task<bool> RemoveSingle(ImageShare resource);
    public Task<byte[]> GetResource(ImageShare resource);
    public Task<string> SaveResource(Guid resourceId, byte[] img, int maxScreenSize, ImageShareSource source);
}