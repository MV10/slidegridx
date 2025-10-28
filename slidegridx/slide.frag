#version 450
precision highp float;

in vec2 fragCoord;
uniform sampler2D slide;
uniform vec2 resolution;
uniform vec2 imagesize;
uniform int sizemode;
out vec4 fragColor;

void main()
{
    //vec2 flipped = vec2(fragCoord.x, 1.0 - fragCoord.y);
    //fragColor = vec4(texture(slide, flipped));

    // fragCoord is already normalized: [0,1] x [0,1] over the display area
    vec2 uv = fragCoord;

    // Compute target display rectangle size (in normalized units)
    vec2 target_size_norm;
    if (sizemode == 0) {
        // Fit to longest dimension, preserve aspect ratio
        float scale = max(resolution.x / imagesize.x, resolution.y / imagesize.y);
        vec2 scaled = imagesize * scale;
        target_size_norm = scaled / resolution;
    }
    else if (sizemode == 1) {
        // Fit to width
        float scale = resolution.x / imagesize.x;
        vec2 scaled = vec2(resolution.x, imagesize.y * scale);
        target_size_norm = scaled / resolution;
    }
    else // sizemode == 2
    {
        // Fit to height
        float scale = resolution.y / imagesize.y;
        vec2 scaled = vec2(imagesize.x * scale, resolution.y);
        target_size_norm = scaled / resolution;
    }

    // Center offset in normalized coordinates
    vec2 offset_norm = (vec2(1.0, 1.0) - target_size_norm) * 0.5;

    // Image rectangle in normalized space
    vec2 rect_min = offset_norm;
    vec2 rect_max = offset_norm + target_size_norm;

    // Check if current fragment is inside the image rectangle
    if (uv.x >= rect_min.x && uv.x < rect_max.x &&
    uv.y >= rect_min.y && uv.y < rect_max.y)
    {
        // Local normalized position within the image rectangle
        vec2 local_uv = uv - rect_min;

        // Normalize to [0,1] within the scaled image
        vec2 img_uv = local_uv / target_size_norm;

        // Map to texture coordinates [0,1] x [0,1], vertically flipped
        vec2 tex_uv = vec2(img_uv.x, 1.0 - img_uv.y);

        // Sample the texture
        fragColor = texture(slide, tex_uv);
    }
    else
    {
        // Outside image area: output black
        fragColor = vec4(0.0, 0.0, 0.0, 1.0);
    }
    
    fragColor.a = 1.0;
}