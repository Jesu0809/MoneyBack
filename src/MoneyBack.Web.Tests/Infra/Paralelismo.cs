using System.Globalization;

// Las pruebas de este proyecto corren en serie a propósito.
//
// Varias verifican que la app funcione en es-CO, que es la cultura real de
// los teléfonos donde corre. La única forma de que esa cultura llegue al
// componente es CultureInfo.DefaultThreadCurrentCulture, porque bUnit
// renderiza en su propio despachador sobre un hilo del pool — fijarla solo
// en el hilo de la prueba no llega, y la prueba pasaba sin probar nada.
//
// Pero esa propiedad es global al proceso: con paralelismo, una prueba de
// cultura le cambia la cultura a otra que esté corriendo al mismo tiempo, y
// las fallas salen intermitentes y en la prueba equivocada (pasó: rompió el
// registro de aportes sin que hubiera nada malo en esa pantalla).
//
// Cuesta medio segundo en toda la suite. Una prueba que falla una de cada
// tres veces cuesta mucho más que eso.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MoneyBack.Web.Tests.Infra;

public static class Cultura
{
    /// <summary>
    /// Corre algo como si el teléfono estuviera en español de Colombia.
    /// </summary>
    public static void ComoEnColombia(Action prueba)
    {
        var anteriorDefecto = CultureInfo.DefaultThreadCurrentCulture;
        var anteriorHilo = CultureInfo.CurrentCulture;
        var esCO = new CultureInfo("es-CO");
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = esCO;
            CultureInfo.CurrentCulture = esCO;
            prueba();
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = anteriorDefecto;
            CultureInfo.CurrentCulture = anteriorHilo;
        }
    }
}
