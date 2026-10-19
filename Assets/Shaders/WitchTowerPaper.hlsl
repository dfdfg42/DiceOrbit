#ifndef WITCH_TOWER_PAPER_INCLUDED
#define WITCH_TOWER_PAPER_INCLUDED

float TowerHash(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float TowerNoise(float3 p)
{
    float3 cell = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float lo = lerp(lerp(TowerHash(cell), TowerHash(cell + float3(1,0,0)), f.x),
                    lerp(TowerHash(cell + float3(0,1,0)), TowerHash(cell + float3(1,1,0)), f.x), f.y);
    float hi = lerp(lerp(TowerHash(cell + float3(0,0,1)), TowerHash(cell + float3(1,0,1)), f.x),
                    lerp(TowerHash(cell + float3(0,1,1)), TowerHash(cell + float3(1,1,1)), f.x), f.y);
    return lerp(lo, hi, f.z);
}

// World-anchored pigment; derivative filtering prevents fine paper grain flickering at a distance.
float TowerPigment(float3 positionWS)
{
    float broad = TowerNoise(positionWS * 1.8) - 0.5;
    float fineVisibility = 1.0 - smoothstep(0.08, 0.24, length(fwidth(positionWS)));
    float grain = (TowerNoise(positionWS * 28.0) - 0.5) * fineVisibility;
    return broad * 0.8 + grain * 0.6;
}
#endif
