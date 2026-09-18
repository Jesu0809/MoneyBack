// Primer service worker real del proyecto. El "service-worker.js" que
// Firebase Hosting sirve por defecto es solo el fallback de reescritura de
// la SPA (sirve index.html), no uno registrado — este sí se registra desde
// PushNotificationService.cs vía navigator.serviceWorker.register().
//
// Solo maneja push/notificationclick. No cachea nada a propósito: MoneyBack
// ya tiene su propia estrategia de caché por fingerprinting en los archivos
// estáticos (ver Scripts/fingerprint-css.sh) — un service worker que además
// cachea el shell de la app agregaría una segunda capa de caché que puede
// volver a producir el mismo tipo de bug de "versión vieja pegada" que ya
// se resolvió ahí, sin necesidad real para esta app.

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
