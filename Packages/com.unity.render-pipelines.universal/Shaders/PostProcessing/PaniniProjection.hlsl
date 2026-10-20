#ifndef UNIVERSAL_POSTPROCESSING_PANINI_INCLUDED
#define UNIVERSAL_POSTPROCESSING_PANINI_INCLUDED

float PaniniProjectionScale(float viewX, float distance)
{
    float scale = 1.0;

    if (abs(distance - 1.0) <= 1e-5)
    {
        scale = 4.0 / max(4.0 - viewX * viewX, 1e-6);
    }
    else
    {
        float viewDistance = 1.0 + distance;
        float viewHypSq = max(viewX * viewX + viewDistance * viewDistance, 1e-6);
        float intersectionD = viewX * distance;
        float intersectionDiscriminant = max(viewHypSq - intersectionD * intersectionD, 0.0);
        float cylinderDistanceMinusD =
            (-intersectionD * viewX + viewDistance * sqrt(intersectionDiscriminant)) / viewHypSq;
        cylinderDistanceMinusD = max(cylinderDistanceMinusD, 1e-6);
        float cylinderDistance = cylinderDistanceMinusD + distance;
        scale = (cylinderDistance / viewDistance) / cylinderDistanceMinusD;
    }

    return scale;
}

void BuildPaniniProjectionColumn(float u, float4 paniniParams, float paniniEnabled, out float2 column)
{
    column = float2(u, 1.0);

    if (paniniEnabled >= 0.5)
    {
        float viewX = (2.0 * u - 1.0) * paniniParams.x * paniniParams.w;
        float projectionScale = PaniniProjectionScale(viewX, paniniParams.z);
        column.x = viewX * projectionScale / max(abs(paniniParams.x), 1e-6) * 0.5 + 0.5;
        column.y = paniniParams.w * projectionScale;
    }
}

float2 ApplyPaniniProjectionColumn(float2 column, float v)
{
    return float2(column.x, (v - 0.5) * column.y + 0.5);
}

float2 ApplyPaniniProjection(float2 uv, float4 paniniParams, float paniniEnabled)
{
    float2 column;
    BuildPaniniProjectionColumn(uv.x, paniniParams, paniniEnabled, column);
    return ApplyPaniniProjectionColumn(column, uv.y);
}

#endif
