export async function postUploadImage(image: File, csrfToken: string): Promise<boolean> {

    const SITE_BASE = import.meta.env.VITE_SITE_BASE_URL;

    const formData = new FormData();
    formData.append('image', image);

    const response = await fetch(`${SITE_BASE}/api/upload/post-images`,
        {
            method: 'POST',
            body: formData,
            credentials: 'include',
            headers: {
                "X-CSRF-TOKEN": csrfToken
            }
        }
    )

    return response.ok;
}
