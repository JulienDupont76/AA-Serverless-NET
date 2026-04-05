using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Julien.Function;

public class ResizeImage
{
    private readonly ILogger<ResizeImage> _logger;
    
    // Limites raisonnables pour éviter les abus
    private const int MaxWidth = 4000;
    private const int MaxHeight = 4000;
    private const int MinWidth = 1;
    private const int MinHeight = 1;
    private const long MaxUploadSizeBytes = 50 * 1024 * 1024; // 50 MB

    public ResizeImage(ILogger<ResizeImage> logger)
    {
        _logger = logger;
    }

    [Function("ResizeImage")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req)
    {
        try
        {
            _logger.LogInformation("Resize image function triggered");

            // Valide que le corps de la requête n'est pas vide
            if (req.ContentLength == 0 || req.Body.Length == 0)
            {
                _logger.LogWarning("Empty request body");
                return new BadRequestObjectResult("Le corps de la requête ne doit pas être vide.");
            }

            // Vérifie la taille maximale
            if (req.ContentLength > MaxUploadSizeBytes)
            {
                _logger.LogWarning($"Request body too large: {req.ContentLength} bytes");
                return new BadRequestObjectResult(
                    $"La taille du fichier dépasse {MaxUploadSizeBytes / (1024 * 1024)} MB.");
            }

            // Récupère et valide les paramètres w et h
            if (!req.Query.TryGetValue("w", out var wValue) || 
                !int.TryParse(wValue.ToString(), out int width))
            {
                _logger.LogWarning("Missing or invalid 'w' parameter");
                return new BadRequestObjectResult("Le paramètre 'w' (largeur) est obligatoire et doit être un entier.");
            }

            if (!req.Query.TryGetValue("h", out var hValue) || 
                !int.TryParse(hValue.ToString(), out int height))
            {
                _logger.LogWarning("Missing or invalid 'h' parameter");
                return new BadRequestObjectResult("Le paramètre 'h' (hauteur) est obligatoire et doit être un entier.");
            }

            // Valide les dimensions
            if (width < MinWidth || height < MinHeight || width > MaxWidth || height > MaxHeight)
            {
                _logger.LogWarning($"Invalid dimensions: w={width}, h={height}");
                return new BadRequestObjectResult(
                    $"Les dimensions doivent être entre {MinWidth}x{MinHeight} et {MaxWidth}x{MaxHeight}.");
            }

            _logger.LogInformation($"Resizing image to {width}x{height}");

            // Charge et redimensionne l'image
            byte[] targetImageBytes;
            using (var msInput = new MemoryStream())
            {
                // Copie le corps de la requête en mémoire
                req.Body.CopyTo(msInput);
                msInput.Position = 0;

                try
                {
                    // Charge l'image depuis le stream
                    using (var image = Image.Load(msInput))
                    {
                        _logger.LogInformation($"Original image size: {image.Width}x{image.Height}");

                        // Effectue le redimensionnement
                        image.Mutate(x => x.Resize(width, height));

                        // Sauvegarde en mémoire au format JPEG
                        using (var msOutput = new MemoryStream())
                        {
                            image.SaveAsJpeg(msOutput);
                            targetImageBytes = msOutput.ToArray();
                        }
                    }
                }
                catch (UnknownImageFormatException)
                {
                    _logger.LogWarning("Invalid image format in request body");
                    return new BadRequestObjectResult("Le fichier envoyé n'est pas un format d'image valide (JPEG, PNG, BMP, etc.).");
                }
                catch (ImageProcessingException ex)
                {
                    _logger.LogError($"Image processing error: {ex.Message}");
                    return new BadRequestObjectResult("Erreur lors du traitement de l'image.");
                }
            }

            _logger.LogInformation($"Resized image size: {targetImageBytes.Length} bytes");

            // Renvoie l'image redimensionnée avec le bon content-type
            return new FileContentResult(targetImageBytes, "image/jpeg")
            {
                FileDownloadName = "resized.jpeg"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unhandled exception: {ex.Message}");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}