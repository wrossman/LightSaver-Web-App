export interface DeviceUploadStatus {
    isUploading: boolean,
    currentUploaded: number,
    totalImages: number,
    error: string | null
}