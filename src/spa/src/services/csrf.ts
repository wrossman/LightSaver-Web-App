const SITE_BASE = import.meta.env.VITE_SITE_BASE_URL;
let requestToken: string = "";

interface CsrfResponse {
    token: string
};

export async function getCsrfToken() {
    if (requestToken) return requestToken;

    const response = await fetch(`${SITE_BASE}/api/security/csrf`, {
        method: 'GET',
        credentials: "include"
    });

    const responseJson = await response.json() as CsrfResponse;

    requestToken = responseJson.token;

    console.log(requestToken);

    return requestToken;
}