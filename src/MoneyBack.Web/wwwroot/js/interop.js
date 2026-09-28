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

// --- Globo en el ícono de la app ---
// La isla dinámica está fuera del alcance de una PWA: las Live Activities
// exigen ActivityKit, que solo existe en apps nativas. Pero el globo rojo
// sobre el ícono sí funciona en iOS 16.4+ cuando la app está en la pantalla
// de inicio, y sirve para lo mismo que uno querría de la isla: ver que hay
// algo pendiente sin abrir nada.
//
// Se usa para los gastos sin clasificar. Es el único número de la app que
// representa algo que la persona tiene que hacer; ponerle un globo a
// cualquier otra cosa sería entrenarla a ignorarlo.
export async function marcarPendientes(cantidad) {
    try {
        if (!("setAppBadge" in navigator)) return false;

        if (cantidad > 0) await navigator.setAppBadge(cantidad);
        else await navigator.clearAppBadge();

        return true;
    } catch {
        // En iOS falla si no se ha concedido permiso de notificaciones, y
        // en escritorio si la app no está instalada. Ninguno de los dos es
        // un problema que valga la pena mostrarle a nadie.
        return false;
    }
}

// Avisa a Blazor cuándo el dedo está bajando por la página, para que el
// botón flotante se aparte. Se hace en JS y no en C# porque un evento de
// scroll dispara decenas de veces por segundo y cruzarlos todos a .NET por
// interop sería carísimo: acá solo cruza el cambio de estado.
window.moneyback = window.moneyback || {};

window.moneyback.vigilarScroll = (referencia) => {
    let ultimo = window.scrollY;
    let apartado = false;
    let quieto;

    const avisar = (valor) => {
        if (valor === apartado) return;
        apartado = valor;
        referencia.invokeMethodAsync("AlCambiarDireccionDelScroll", valor);
    };

    const alScrollear = () => {
        const actual = window.scrollY;

        // Un umbral de 6px para que el rebote del scroll elástico de iOS no
        // encienda y apague el botón mientras el dedo está quieto.
        if (actual > ultimo + 6 && actual > 120) avisar(true);
        else if (actual < ultimo - 6) avisar(false);

        ultimo = actual;

        // Al detenerse vuelve entero: si alguien paró de bajar, muy
        // probablemente es porque ya va a tocar algo.
        clearTimeout(quieto);
        quieto = setTimeout(() => avisar(false), 900);
    };

    window.addEventListener("scroll", alScrollear, { passive: true });
    return () => window.removeEventListener("scroll", alScrollear);
};
