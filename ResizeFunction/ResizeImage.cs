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
    private const int MaxWidth = 4000;
    private const int MaxHeight = 4000;
    private const int MinWidth = 1;
    private const int MinHeight = 1;
    private const long MaxUploadSizeBytes = 50 * 1024 * 1024;

    public ResizeImage(ILogger<ResizeImage> logger)
    {
        _logger = logger;
    }

    [Function("ResizeImage")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req)
    {
        try
        {
            _logger.LogInformation("Resize image function triggered");

            if (req.ContentLength == 0 || req.ContentLength == 0)
            {
                _logger.LogWarning("Empty request body");
                return new BadRequestObjectResult("Le corps de la requête ne doit pas être vide.");
            }

            if (req.ContentLength > MaxUploadSizeBytes)
            {
                _logger.LogWarning($"Request body too large: {req.ContentLength} bytes");
                return new BadRequestObjectResult(
                    $"La taille du fichier dépasse {MaxUploadSizeBytes / (1024 * 1024)} MB.");
            }

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

            if (width < MinWidth || height < MinHeight || width > MaxWidth || height > MaxHeight)
            {
                _logger.LogWarning($"Invalid dimensions: w={width}, h={height}");
                return new BadRequestObjectResult(
                    $"Les dimensions doivent être entre {MinWidth}x{MinHeight} et {MaxWidth}x{MaxHeight}.");
            }

            _logger.LogInformation($"Resizing image to {width}x{height}");

            byte[] targetImageBytes;
            using (var msInput = new MemoryStream())
            {
                // Récupère le corps du message en mémoire
                await req.Body.CopyToAsync(msInput);
                msInput.Position = 0;

                try
                {
                    // Charge l'image 
                    using (var image = Image.Load(msInput))
                    {
                        _logger.LogInformation($"Original image size: {image.Width}x{image.Height}");

                        // Effectue la transformation
                        image.Mutate(x => x.Resize(width, height));

                        // Sauvegarde en mémoire 
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

            // Renvoie le contenu avec le content-type correspondant à une image jpeg
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