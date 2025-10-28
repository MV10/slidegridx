#version 450
precision highp float;

in vec2 fragCoord;
uniform sampler2D slide;
uniform vec2 resolution;
uniform vec2 imagesize;
uniform int sizemode; // TODO: make the modes fit all, fit largest, fit width, or fit height
out vec4 fragColor;

void main()
{
    // Normalized display coordinates [0,1] x [0,1]
    vec2 uv = fragCoord;

    // Compute aspect ratios
    float display_aspect = resolution.x / resolution.y;
    float image_aspect   = imagesize.x / imagesize.y;

    // Determine scale factor to fit image within display while preserving aspect ratio
    float scale;
    if (image_aspect > display_aspect) {
        // Image is wider: fit to width
        scale = resolution.x / imagesize.x;
    } else {
        // Image is taller or square: fit to height
        scale = resolution.y / imagesize.y;
    }

    // Scaled image dimensions in pixels
    vec2 scaled_size = imagesize * scale;

    // Normalized size in display space
    vec2 target_size_norm = scaled_size / resolution;

    // Center the image
    vec2 offset_norm = (vec2(1.0, 1.0) - target_size_norm) * 0.5;

    // Bounds of the centered image rectangle
    vec2 rect_min = offset_norm;
    vec2 rect_max = offset_norm + target_size_norm;

    // Check if fragment is inside the image area
    if (uv.x >= rect_min.x && uv.x < rect_max.x &&
    uv.y >= rect_min.y && uv.y < rect_max.y)
    {
        // Local coordinates within the scaled image rectangle
        vec2 local_uv = uv - rect_min;

        // Map to texture coordinates [0,1] x [0,1]
        vec2 tex_uv_unflipped = local_uv / target_size_norm;

        // Apply vertical flip
        vec2 tex_uv = vec2(tex_uv_unflipped.x, 1.0 - tex_uv_unflipped.y);

        // Sample texture
        fragColor = texture(slide, tex_uv);
    }
    else
    {
        // Outside image area
        fragColor = vec4(0.0, 0.0, 0.0, 1.0);
    }
}
