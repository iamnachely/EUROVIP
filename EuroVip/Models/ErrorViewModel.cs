namespace EuroVip.Models;

/// <summary>Datos de la página de error (Views/Shared/Error.cshtml). Textos con temática de viajes.</summary>
public class ErrorViewModel
{
    public int Codigo { get; set; } = 500;
    public string Titulo { get; set; } = "";
    public string Mensaje { get; set; } = "";
    /// <summary>Ilustración: avion (500), mapa (404), candado (401/403), reloj (408/429/503/504), maleta (resto).</summary>
    public string Dibujo { get; set; } = "avion";
    public string Referencia { get; set; } = "";
    public string Ruta { get; set; } = "";
    /// <summary>Solo en Development: la excepción completa.</summary>
    public string? Detalle { get; set; }
    public bool PuedeReintentar => Codigo is 408 or 429 or >= 500;

    public static ErrorViewModel Para(int codigo, string referencia, string ruta)
    {
        var (titulo, mensaje, dibujo) = codigo switch
        {
            400 => ("Esa solicitud no se entendió", "Algún dato llegó incompleto o con un formato raro. Vuelve atrás y revisa lo que escribiste.", "maleta"),
            401 => ("Tienes que iniciar sesión", "Tu sesión terminó. Entra de nuevo para seguir trabajando.", "candado"),
            403 => ("Zona con acceso restringido", "No tienes permiso para esta parte de Eurovip. Pídeselo a un administrador.", "candado"),
            404 => ("Esta página se fue de vacaciones", "No encontramos lo que buscas. Puede que la dirección esté mal escrita o que ya no exista.", "mapa"),
            405 => ("Por aquí no se puede pasar", "Esa acción no está permitida en esta página.", "maleta"),
            408 => ("Se nos fue el tiempo", "La página tardó demasiado en responder. Inténtalo otra vez.", "reloj"),
            413 => ("Equipaje con sobrepeso", "El archivo que intentaste subir es demasiado grande.", "maleta"),
            429 => ("Demasiados pasajeros a la vez", "Hiciste muchas solicitudes seguidas. Espera unos segundos y vuelve a intentarlo.", "reloj"),
            502 or 504 => ("Escala con retraso", "Un servicio que usamos no respondió a tiempo. Inténtalo de nuevo en un momento.", "reloj"),
            503 => ("Estamos en mantenimiento", "Eurovip no está disponible en este momento. Vuelve a intentarlo en unos minutos.", "reloj"),
            _ => ("Turbulencias en el sistema", "Ha ocurrido un error en la aplicación. Si vuelve a pasar, contacta con el Departamento de Informática y dales el código de abajo.", "avion"),
        };
        return new ErrorViewModel { Codigo = codigo, Titulo = titulo, Mensaje = mensaje, Dibujo = dibujo, Referencia = referencia, Ruta = ruta };
    }
}
