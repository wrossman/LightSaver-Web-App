export function cookiesEnabled(): boolean {
    const name = `cookie_test_${Date.now()}`;

    document.cookie = `${name}=1; path=/`;

    const enabled = document.cookie.includes(`${name}=`);

    document.cookie = `${name}=; Max-Age=0; path=/`;

    return enabled;
}