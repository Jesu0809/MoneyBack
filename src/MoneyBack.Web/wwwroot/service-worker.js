// Service worker de MoneyBack: push + caché de arranque.
//
// Sobre la caché: la versión anterior de este archivo no cacheaba nada a
// propósito, por miedo a reintroducir un bug de "versión vieja pegada". Esa
// preocupación es válida pero solo aplica a los archivos cuyo nombre NO
// cambia entre despliegues — es decir, index.html. Todo lo que vive en
// _framework/ y css/app.<hash>.css lleva el hash del contenido en el nombre
// (ver fingerprint-css.sh y el pipeline de Blazor), así que un despliegue
// nuevo produce nombres nuevos: es imposible servir código viejo desde la
// caché, porque nadie vuelve a pedir esos nombres.
//
// Por eso la estrategia es mixta:
//   - index.html  -> red primero, caché solo como respaldo sin señal.
//   - archivos con hash -> caché primero (son inmutables por definición).
//   - API (otro dominio) -> nunca se cachea; la plata siempre se lee fresca.

const VERSION = "v1";
const CACHE = `moneyback-${VERSION}`;

self.addEventListener("install", (event) => {
    // Se guarda el shell de entrada desde ya: si la primera vez que abres sin
    // señal es en una ruta profunda (/hogar, /mas...), el respaldo necesita
    // tener algo que servir aunque esa ruta nunca se haya visitado antes.
    event.waitUntil(
        caches.open(CACHE)
            .then((cache) => cache.addAll(["/", "/index.html"]))
            .catch(() => { /* sin señal al instalar: se llenará al primer fetch */ })
    );
    // Toma el control sin esperar a que se cierren las pestañas viejas.
    self.skipWaiting();
});

self.addEventListener("activate", (event) => {
    event.waitUntil((async () => {
        const nombres = await caches.keys();
        await Promise.all(nombres.filter((n) => n !== CACHE).map((n) => caches.delete(n)));
        await self.clients.claim();
    })());
});

/// Un archivo es inmutable si su nombre lleva el hash del contenido: si el
/// contenido cambia, el nombre cambia, así que la caché nunca queda vieja.
function esInmutable(pathname) {
    return pathname.startsWith("/_framework/") || /^\/css\/app\.[a-z0-9]+\.css$/i.test(pathname);
}

async function cachePrimero(request) {
    const enCache = await caches.match(request);
    if (enCache) return enCache;

    const respuesta = await fetch(request);
    if (respuesta.ok) {
        const cache = await caches.open(CACHE);
        cache.put(request, respuesta.clone());
    }
    return respuesta;
}

async function redPrimero(request) {
    try {
        const respuesta = await fetch(request);
        if (respuesta.ok) {
            const cache = await caches.open(CACHE);
            cache.put(request, respuesta.clone());
        }
        return respuesta;
    } catch (error) {
        // Sin señal: servir lo último que se guardó para que la app abra igual.
        // Cualquier ruta cae al shell — Blazor resuelve el enrutamiento del
        // lado del cliente una vez carga, igual que hace el rewrite de Firebase.
        const enCache = await caches.match(request);
        if (enCache) return enCache;
        const shell = (await caches.match("/")) || (await caches.match("/index.html"));
        if (shell) return shell;
        throw error;
    }
}

self.addEventListener("fetch", (event) => {
    const request = event.request;

    // Solo GET: un POST de movimiento nunca debe salir de una caché.
    if (request.method !== "GET") return;

    const url = new URL(request.url);

    // La API vive en otro dominio (fly.dev): se deja pasar sin tocar, para
    // que los saldos y movimientos siempre vengan del servidor.
    if (url.origin !== self.location.origin) return;

    if (request.mode === "navigate") {
        event.respondWith(redPrimero(request));
        return;
    }

    if (esInmutable(url.pathname)) {
        event.respondWith(cachePrimero(request));
        return;
    }

    // Íconos, manifest y demás estáticos sin hash: caché con respaldo de red.
    event.respondWith(cachePrimero(request));
});

self.addEventListener("push", (event) => {
    let datos = { titulo: "MoneyBack", cuerpo: "Tienes una notificación nueva." };
    if (event.data) {
        try { datos = event.data.json(); } catch { datos.cuerpo = event.data.text(); }
    }

    event.waitUntil(
        self.registration.showNotification(datos.titulo, {
            body: datos.cuerpo,
            icon: "/icon-192.png",
            badge: "/icon-192.png",
            data: { url: datos.url || "/" }
        })
    );
});

self.addEventListener("notificationclick", (event) => {
    event.notification.close();
    const url = event.notification.data?.url || "/";

    event.waitUntil(
        clients.matchAll({ type: "window", includeUncontrolled: true }).then((lista) => {
            for (const cliente of lista) {
                if (cliente.url.includes(self.registration.scope) && "focus" in cliente) {
                    cliente.navigate(url);
                    return cliente.focus();
                }
            }
            return clients.openWindow(url);
        })
    );
});
