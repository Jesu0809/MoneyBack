// Primer archivo de JS interop del proyecto. Hasta ahora ThemeService llamaba
// directo a globals del navegador (localStorage, document.documentElement)
// vía IJSRuntime sin necesitar un módulo propio — esto sí lo necesita, porque
// descargar un archivo requiere una secuencia de pasos (Blob, object URL, <a>
// sintético) que no tiene un equivalente de una sola llamada.
//
// Por qué existe esto en vez de un <a href> directo al endpoint del API: el
// token de autenticación (JWT) solo se adjunta a pedidos hechos con el
// HttpClient nombrado "Api" (vía AuthorizedHttpMessageHandler, del lado de
// Blazor) — una navegación de navegador a una URL nunca pasa por ahí y
// llegaría sin Authorization, así que el API respondería 401. Por eso
// ApiClient pide los bytes ya autenticado y esta función solo se encarga de
// la descarga real en el navegador con los bytes que ya llegaron.
export function downloadFile(bytes, nombreArchivo, tipoMime) {
    const blob = new Blob([new Uint8Array(bytes)], { type: tipoMime });
    const url = URL.createObjectURL(blob);
    const enlace = document.createElement("a");
    enlace.href = url;
    enlace.download = nombreArchivo;
    document.body.appendChild(enlace);
    enlace.click();
    document.body.removeChild(enlace);
    URL.revokeObjectURL(url);
}

export async function copiarAlPortapapeles(texto) {
    try {
        await navigator.clipboard.writeText(texto);
        return true;
    } catch {
        return false;
    }
}

// --- Notificaciones push ---
// pushManager.subscribe() exige la llave VAPID como Uint8Array, pero el
// backend la entrega en base64url (formato estándar de VAPID) — este es el
// conversor de una sola vía que traduce entre los dos formatos.
function base64UrlAUint8Array(base64Url) {
    const padding = "=".repeat((4 - (base64Url.length % 4)) % 4);
    const base64 = (base64Url + padding).replace(/-/g, "+").replace(/_/g, "/");
    const raw = atob(base64);
    return Uint8Array.from([...raw].map((c) => c.charCodeAt(0)));
}

export function notificacionesSoportadas() {
    return "serviceWorker" in navigator && "PushManager" in window && "Notification" in window;
}

export function estadoPermisoNotificaciones() {
    return notificacionesSoportadas() ? Notification.permission : "unsupported";
}

// Devuelve { endpoint, p256dh, auth } o null si el usuario negó el permiso o
// el navegador rechazó el registro (ej. modo incógnito, sin conexión). El
// permiso SOLO se puede pedir a raíz de un gesto del usuario (un click) —
// por eso esto se llama desde un botón "Activar notificaciones", nunca solo.
// Atrapa cualquier error de las llamadas al navegador: si esto lanza sin
// capturar, Blazor lo trata como excepción no manejada y tumba el render
// completo del componente, no solo esta acción puntual.
export async function pedirPermisoYSuscribirse(vapidPublicKeyBase64) {
    try {
        if (!notificacionesSoportadas()) return null;

        const permiso = await Notification.requestPermission();
        if (permiso !== "granted") return null;

        const registro = await navigator.serviceWorker.register("/service-worker.js");
        await navigator.serviceWorker.ready;

        let suscripcion = await registro.pushManager.getSubscription();
        if (!suscripcion) {
            suscripcion = await registro.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: base64UrlAUint8Array(vapidPublicKeyBase64)
            });
        }

        const json = suscripcion.toJSON();
        return { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth };
    } catch (error) {
        console.error("No se pudo activar notificaciones push:", error);
        return null;
    }
}

export async function estaSuscritoNotificaciones() {
    if (!notificacionesSoportadas()) return false;
    const registro = await navigator.serviceWorker.getRegistration();
    const suscripcion = await registro?.pushManager.getSubscription();
    return !!suscripcion;
}

export async function desuscribirseNotificaciones() {
    try {
        if (!notificacionesSoportadas()) return null;
        const registro = await navigator.serviceWorker.getRegistration();
        const suscripcion = await registro?.pushManager.getSubscription();
        if (!suscripcion) return null;

        const json = suscripcion.toJSON();
        await suscripcion.unsubscribe();
        return { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth };
    } catch (error) {
        console.error("No se pudo desactivar notificaciones push:", error);
        return null;
    }
}
