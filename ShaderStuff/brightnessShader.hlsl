sampler2D R : register(s0);
sampler2D G : register(s1);
sampler2D B : register(s2);
float brightness : register(c0);

float4 main(float2 uv : TEXCOORD) : COLOR
{
	float RVal = tex2D(R, uv).r;
	float GVal = tex2D(G, uv).g;
	float BVal = tex2D(B, uv).b;
	float3 output = float3(RVal, GVal, BVal);
	return float4(pow(output, brightness), 1.0f);
}