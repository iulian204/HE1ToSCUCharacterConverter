# Shader inventory

152 base programs: 99 Generations, 53 Unleashed. Four exact aliases are mapped below; they are not additional implementations. Each program has 24 native draw states.

Each linked page lists every parameter in its Opaque, Punch and Trans interface, declarations/defaults, reachable stage/helper uses, source operations, source-list links, alpha expression, foundation changes and validation boundaries. Blend/sidedness presets share the interface of their coverage layer.

| Game | Shader | Environment | Parameters O/P/T | Reference |
|---|---|---|---|---|
| Generations | `ChrEye_dpne1@@` | sphere / source 2D projection (see sampling expressions) | 26/28/27 | [Details](Shaders/Generations/ChrEye_dpne1@@.md) |
| Generations | `ChrEye_dpne2@@` | 4x3 cubemap cross | 27/29/28 | [Details](Shaders/Generations/ChrEye_dpne2@@.md) |
| Generations | `ChrEye_dpne@@` | dual sphere / dual paraboloid (see sampling expressions) | 26/28/27 | [Details](Shaders/Generations/ChrEye_dpne@@.md) |
| Generations | `ChrSkinHalf_dse11@@` | sphere / source 2D projection (see sampling expressions) | 22/24/23 | [Details](Shaders/Generations/ChrSkinHalf_dse11@@.md) |
| Generations | `ChrSkinIgnore_dsle11@@` | sphere / source 2D projection (see sampling expressions) | 25/27/26 | [Details](Shaders/Generations/ChrSkinIgnore_dsle11@@.md) |
| Generations | `ChrSkin_ds@@` | none | 21/23/22 | [Details](Shaders/Generations/ChrSkin_ds@@.md) |
| Generations | `ChrSkin_dse1@@` | sphere / source 2D projection (see sampling expressions) | 23/25/24 | [Details](Shaders/Generations/ChrSkin_dse1@@.md) |
| Generations | `ChrSkin_dse2@@` | 4x3 cubemap cross | 24/26/25 | [Details](Shaders/Generations/ChrSkin_dse2@@.md) |
| Generations | `ChrSkin_dse@@` | dual sphere / dual paraboloid (see sampling expressions) | 23/25/24 | [Details](Shaders/Generations/ChrSkin_dse@@.md) |
| Generations | `ChrSkin_dsf@@` | none | 23/25/24 | [Details](Shaders/Generations/ChrSkin_dsf@@.md) |
| Generations | `ChrSkin_dsfe1@@` | sphere / source 2D projection (see sampling expressions) | 25/27/26 | [Details](Shaders/Generations/ChrSkin_dsfe1@@.md) |
| Generations | `ChrSkin_dsfe2@@` | 4x3 cubemap cross | 26/28/27 | [Details](Shaders/Generations/ChrSkin_dsfe2@@.md) |
| Generations | `ChrSkin_dsfe@@` | dual sphere / dual paraboloid (see sampling expressions) | 25/27/26 | [Details](Shaders/Generations/ChrSkin_dsfe@@.md) |
| Generations | `ChrSkin_dsn@@` | none | 23/25/24 | [Details](Shaders/Generations/ChrSkin_dsn@@.md) |
| Generations | `ChrSkin_dsne1@@` | sphere / source 2D projection (see sampling expressions) | 25/27/26 | [Details](Shaders/Generations/ChrSkin_dsne1@@.md) |
| Generations | `ChrSkin_dsne@@` | dual sphere / dual paraboloid (see sampling expressions) | 25/27/26 | [Details](Shaders/Generations/ChrSkin_dsne@@.md) |
| Generations | `ChrSkin_dsnf@@` | none | 25/27/26 | [Details](Shaders/Generations/ChrSkin_dsnf@@.md) |
| Generations | `ChrSkin_dsnfe1@@` | sphere / source 2D projection (see sampling expressions) | 27/29/28 | [Details](Shaders/Generations/ChrSkin_dsnfe1@@.md) |
| Generations | `ChrSkin_dsnfe@@` | dual sphere / dual paraboloid (see sampling expressions) | 27/29/28 | [Details](Shaders/Generations/ChrSkin_dsnfe@@.md) |
| Generations | `Common_d@@` | none | 22/24/23 | [Details](Shaders/Generations/Common_d@@.md) |
| Generations | `Common_da@@` | none | 22/26/25 | [Details](Shaders/Generations/Common_da@@.md) |
| Generations | `Common_de1@@` | sphere / source 2D projection (see sampling expressions) | 24/26/25 | [Details](Shaders/Generations/Common_de1@@.md) |
| Generations | `Common_de2@@` | 4x3 cubemap cross | 25/27/26 | [Details](Shaders/Generations/Common_de2@@.md) |
| Generations | `Common_de@@` | dual sphere / dual paraboloid (see sampling expressions) | 24/26/25 | [Details](Shaders/Generations/Common_de@@.md) |
| Generations | `Common_dn@@` | none | 24/26/25 | [Details](Shaders/Generations/Common_dn@@.md) |
| Generations | `Common_dne1@@` | sphere / source 2D projection (see sampling expressions) | 26/28/27 | [Details](Shaders/Generations/Common_dne1@@.md) |
| Generations | `Common_dne2@@` | 4x3 cubemap cross | 27/29/28 | [Details](Shaders/Generations/Common_dne2@@.md) |
| Generations | `Common_dne@@` | dual sphere / dual paraboloid (see sampling expressions) | 26/28/27 | [Details](Shaders/Generations/Common_dne@@.md) |
| Generations | `Common_dp@@` | none | 26/28/27 | [Details](Shaders/Generations/Common_dp@@.md) |
| Generations | `Common_dp@E@` | none | 27/29/28 | [Details](Shaders/Generations/Common_dp@E@.md) |
| Generations | `Common_dpe1@@` | sphere / source 2D projection (see sampling expressions) | 28/30/29 | [Details](Shaders/Generations/Common_dpe1@@.md) |
| Generations | `Common_dpe2@@` | 4x3 cubemap cross | 29/31/30 | [Details](Shaders/Generations/Common_dpe2@@.md) |
| Generations | `Common_dpe@@` | dual sphere / dual paraboloid (see sampling expressions) | 28/30/29 | [Details](Shaders/Generations/Common_dpe@@.md) |
| Generations | `Common_dpn@@` | none | 28/30/29 | [Details](Shaders/Generations/Common_dpn@@.md) |
| Generations | `Common_dpn@E@` | none | 29/31/30 | [Details](Shaders/Generations/Common_dpn@E@.md) |
| Generations | `Common_dpne1@@` | sphere / source 2D projection (see sampling expressions) | 30/32/31 | [Details](Shaders/Generations/Common_dpne1@@.md) |
| Generations | `Common_dpne2@@` | 4x3 cubemap cross | 31/33/32 | [Details](Shaders/Generations/Common_dpne2@@.md) |
| Generations | `Common_dpne@@` | dual sphere / dual paraboloid (see sampling expressions) | 30/32/31 | [Details](Shaders/Generations/Common_dpne@@.md) |
| Generations | `Common_dpnne2@@` | 4x3 cubemap cross | 33/35/34 | [Details](Shaders/Generations/Common_dpnne2@@.md) |
| Generations | `Common_dpnr@@` | none | 29/31/30 | [Details](Shaders/Generations/Common_dpnr@@.md) |
| Generations | `Common_dpr@@` | none | 27/29/28 | [Details](Shaders/Generations/Common_dpr@@.md) |
| Generations | `Common_dsae1@@` | sphere / source 2D projection (see sampling expressions) | 28/32/31 | [Details](Shaders/Generations/Common_dsae1@@.md) |
| Generations | `Common_dse1@@` | sphere / source 2D projection (see sampling expressions) | 28/30/29 | [Details](Shaders/Generations/Common_dse1@@.md) |
| Generations | `Common_dse2@@` | 4x3 cubemap cross | 29/31/30 | [Details](Shaders/Generations/Common_dse2@@.md) |
| Generations | `Common_dse@@` | dual sphere / dual paraboloid (see sampling expressions) | 28/30/29 | [Details](Shaders/Generations/Common_dse@@.md) |
| Generations | `Common_dsn@@` | none | 28/30/29 | [Details](Shaders/Generations/Common_dsn@@.md) |
| Generations | `Common_dsne1@@` | sphere / source 2D projection (see sampling expressions) | 30/32/31 | [Details](Shaders/Generations/Common_dsne1@@.md) |
| Generations | `Common_dsne@@` | dual sphere / dual paraboloid (see sampling expressions) | 30/32/31 | [Details](Shaders/Generations/Common_dsne@@.md) |
| Generations | `Common_dsnne1@@` | sphere / source 2D projection (see sampling expressions) | 32/34/33 | [Details](Shaders/Generations/Common_dsnne1@@.md) |
| Generations | `Common_dsp@@` | none | 28/30/29 | [Details](Shaders/Generations/Common_dsp@@.md) |
| Generations | `Common_dspe@@` | dual sphere / dual paraboloid (see sampling expressions) | 30/32/31 | [Details](Shaders/Generations/Common_dspe@@.md) |
| Generations | `Common_dspn@@` | none | 30/32/31 | [Details](Shaders/Generations/Common_dspn@@.md) |
| Generations | `Common_dspne2@@` | 4x3 cubemap cross | 33/35/34 | [Details](Shaders/Generations/Common_dspne2@@.md) |
| Generations | `Common_dspne@@` | dual sphere / dual paraboloid (see sampling expressions) | 32/34/33 | [Details](Shaders/Generations/Common_dspne@@.md) |
| Generations | `EnmCloud_nfe1@@` | sphere / source 2D projection (see sampling expressions) | 24/26/25 | [Details](Shaders/Generations/EnmCloud_nfe1@@.md) |
| Generations | `EnmEmission_d@@` | none | 16/18/17 | [Details](Shaders/Generations/EnmEmission_d@@.md) |
| Generations | `EnmEmission_dl@@` | none | 19/21/20 | [Details](Shaders/Generations/EnmEmission_dl@@.md) |
| Generations | `EnmEmission_dsl@@` | none | 23/25/24 | [Details](Shaders/Generations/EnmEmission_dsl@@.md) |
| Generations | `EnmEmission_dsle1@@` | sphere / source 2D projection (see sampling expressions) | 25/27/26 | [Details](Shaders/Generations/EnmEmission_dsle1@@.md) |
| Generations | `EnmEmission_dsnl@@` | none | 25/27/26 | [Details](Shaders/Generations/EnmEmission_dsnl@@.md) |
| Generations | `EnmEmission_dsnle1@@` | sphere / source 2D projection (see sampling expressions) | 27/29/28 | [Details](Shaders/Generations/EnmEmission_dsnle1@@.md) |
| Generations | `EnmGlass_dse1@@` | sphere / source 2D projection (see sampling expressions) | 22/24/23 | [Details](Shaders/Generations/EnmGlass_dse1@@.md) |
| Generations | `EnmGlass_dsle1@@` | sphere / source 2D projection (see sampling expressions) | 24/26/25 | [Details](Shaders/Generations/EnmGlass_dsle1@@.md) |
| Generations | `EnmGlass_dsne1@@` | sphere / source 2D projection (see sampling expressions) | 24/26/25 | [Details](Shaders/Generations/EnmGlass_dsne1@@.md) |
| Generations | `EnmGlass_dsnle1@@` | sphere / source 2D projection (see sampling expressions) | 26/28/27 | [Details](Shaders/Generations/EnmGlass_dsnle1@@.md) |
| Generations | `EnmGlass_e1@@` | sphere / source 2D projection (see sampling expressions) | 17/19/18 | [Details](Shaders/Generations/EnmGlass_e1@@.md) |
| Generations | `EnmIgnore_dl@@` | none | 21/23/22 | [Details](Shaders/Generations/EnmIgnore_dl@@.md) |
| Generations | `EnmIgnore_dle1@@` | sphere / source 2D projection (see sampling expressions) | 23/25/24 | [Details](Shaders/Generations/EnmIgnore_dle1@@.md) |
| Generations | `EnmIgnore_dsle1@@` | sphere / source 2D projection (see sampling expressions) | 25/27/26 | [Details](Shaders/Generations/EnmIgnore_dsle1@@.md) |
| Generations | `EnmIgnore_le1@@` | sphere / source 2D projection (see sampling expressions) | 20/22/21 | [Details](Shaders/Generations/EnmIgnore_le1@@.md) |
| Generations | `EnmMetal_dse1@@` | sphere / source 2D projection (see sampling expressions) | 23/25/24 | [Details](Shaders/Generations/EnmMetal_dse1@@.md) |
| Generations | `EnmMetal_dsfe1@@` | sphere / source 2D projection (see sampling expressions) | 25/27/26 | [Details](Shaders/Generations/EnmMetal_dsfe1@@.md) |
| Generations | `EnmMetal_dsne1@@` | sphere / source 2D projection (see sampling expressions) | 25/27/26 | [Details](Shaders/Generations/EnmMetal_dsne1@@.md) |
| Generations | `EnmMetal_dsnfe1@@` | sphere / source 2D projection (see sampling expressions) | 27/29/28 | [Details](Shaders/Generations/EnmMetal_dsnfe1@@.md) |
| Generations | `IgnoreLightTwice_d@v@` | none | 5/7/6 | [Details](Shaders/Generations/IgnoreLightTwice_d@v@.md) |
| Generations | `IgnoreLightV_d@s@` | none | 5/8/7 | [Details](Shaders/Generations/IgnoreLightV_d@s@.md) |
| Generations | `IgnoreLight_d@v@` | none | 5/7/6 | [Details](Shaders/Generations/IgnoreLight_d@v@.md) |
| Generations | `IgnoreLight_dE@v@` | none | 9/11/10 | [Details](Shaders/Generations/IgnoreLight_dE@v@.md) |
| Generations | `IgnoreLight_da@v@` | none | 5/9/8 | [Details](Shaders/Generations/IgnoreLight_da@v@.md) |
| Generations | `LuminescenceV_d@v@` | none | 19/21/20 | [Details](Shaders/Generations/LuminescenceV_d@v@.md) |
| Generations | `LuminescenceV_dn@v@` | none | 21/23/22 | [Details](Shaders/Generations/LuminescenceV_dn@v@.md) |
| Generations | `LuminescenceV_dp@v@` | none | 21/23/22 | [Details](Shaders/Generations/LuminescenceV_dp@v@.md) |
| Generations | `LuminescenceV_dpdpv@v@` | none | 27/29/28 | [Details](Shaders/Generations/LuminescenceV_dpdpv@v@.md) |
| Generations | `LuminescenceV_dpe1@v@` | sphere / source 2D projection (see sampling expressions) | 23/25/24 | [Details](Shaders/Generations/LuminescenceV_dpe1@v@.md) |
| Generations | `LuminescenceV_dpe2@v@` | 4x3 cubemap cross | 24/26/25 | [Details](Shaders/Generations/LuminescenceV_dpe2@v@.md) |
| Generations | `LuminescenceV_dpn@v@` | none | 23/25/24 | [Details](Shaders/Generations/LuminescenceV_dpn@v@.md) |
| Generations | `LuminescenceV_dpndp@v@` | none | 27/29/28 | [Details](Shaders/Generations/LuminescenceV_dpndp@v@.md) |
| Generations | `LuminescenceV_dpnv@v@` | none | 25/27/26 | [Details](Shaders/Generations/LuminescenceV_dpnv@v@.md) |
| Generations | `Luminescence_d@v@` | none | 20/22/21 | [Details](Shaders/Generations/Luminescence_d@v@.md) |
| Generations | `Luminescence_dE@v@` | none | 21/23/22 | [Details](Shaders/Generations/Luminescence_dE@v@.md) |
| Generations | `Luminescence_dnE@v@` | none | 23/25/24 | [Details](Shaders/Generations/Luminescence_dnE@v@.md) |
| Generations | `Luminescence_dne1E@v@` | sphere / source 2D projection (see sampling expressions) | 25/27/26 | [Details](Shaders/Generations/Luminescence_dne1E@v@.md) |
| Generations | `Luminescence_dne2E@v@` | 4x3 cubemap cross | 26/28/27 | [Details](Shaders/Generations/Luminescence_dne2E@v@.md) |
| Generations | `Luminescence_dpE@v@` | none | 23/25/24 | [Details](Shaders/Generations/Luminescence_dpE@v@.md) |
| Generations | `Luminescence_dpe1E@v@` | sphere / source 2D projection (see sampling expressions) | 25/27/26 | [Details](Shaders/Generations/Luminescence_dpe1E@v@.md) |
| Generations | `Luminescence_dpe2E@v@` | 4x3 cubemap cross | 26/28/27 | [Details](Shaders/Generations/Luminescence_dpe2E@v@.md) |
| Generations | `Luminescence_dpnE@v@` | none | 25/27/26 | [Details](Shaders/Generations/Luminescence_dpnE@v@.md) |
| Generations | `Luminescence_dpne1E@v@` | sphere / source 2D projection (see sampling expressions) | 27/29/28 | [Details](Shaders/Generations/Luminescence_dpne1E@v@.md) |
| Generations | `Luminescence_dpne2E@v@` | 4x3 cubemap cross | 28/30/29 | [Details](Shaders/Generations/Luminescence_dpne2E@v@.md) |
| Unleashed | `Common_d@@` | none | 24/26/25 | [Details](Shaders/Unleashed/Common_d@@.md) |
| Unleashed | `Common_d@v@` | none | 24/26/25 | [Details](Shaders/Unleashed/Common_d@v@.md) |
| Unleashed | `Common_de@@` | dual sphere / dual paraboloid (see sampling expressions) | 26/28/27 | [Details](Shaders/Unleashed/Common_de@@.md) |
| Unleashed | `Common_dn@@` | none | 26/28/27 | [Details](Shaders/Unleashed/Common_dn@@.md) |
| Unleashed | `Common_dn@v@` | none | 26/28/27 | [Details](Shaders/Unleashed/Common_dn@v@.md) |
| Unleashed | `Common_dne@@` | dual sphere / dual paraboloid (see sampling expressions) | 28/30/29 | [Details](Shaders/Unleashed/Common_dne@@.md) |
| Unleashed | `Common_dne@c@` | 4x3 cubemap cross | 29/31/30 | [Details](Shaders/Unleashed/Common_dne@c@.md) |
| Unleashed | `Common_dne@cv@` | 4x3 cubemap cross | 29/31/30 | [Details](Shaders/Unleashed/Common_dne@cv@.md) |
| Unleashed | `Common_dp@@` | none | 26/28/27 | [Details](Shaders/Unleashed/Common_dp@@.md) |
| Unleashed | `Common_dp@v@` | none | 26/28/27 | [Details](Shaders/Unleashed/Common_dp@v@.md) |
| Unleashed | `Common_dpe@@` | dual sphere / dual paraboloid (see sampling expressions) | 28/30/29 | [Details](Shaders/Unleashed/Common_dpe@@.md) |
| Unleashed | `Common_dpe@c@` | 4x3 cubemap cross | 29/31/30 | [Details](Shaders/Unleashed/Common_dpe@c@.md) |
| Unleashed | `Common_dpe@v@` | dual sphere / dual paraboloid (see sampling expressions) | 28/30/29 | [Details](Shaders/Unleashed/Common_dpe@v@.md) |
| Unleashed | `Common_dpn@@` | none | 28/30/29 | [Details](Shaders/Unleashed/Common_dpn@@.md) |
| Unleashed | `Common_dpn@v@` | none | 28/30/29 | [Details](Shaders/Unleashed/Common_dpn@v@.md) |
| Unleashed | `Common_dpne@@` | dual sphere / dual paraboloid (see sampling expressions) | 30/32/31 | [Details](Shaders/Unleashed/Common_dpne@@.md) |
| Unleashed | `Common_dpne@c@` | 4x3 cubemap cross | 31/33/32 | [Details](Shaders/Unleashed/Common_dpne@c@.md) |
| Unleashed | `Common_dpne@v@` | dual sphere / dual paraboloid (see sampling expressions) | 30/32/31 | [Details](Shaders/Unleashed/Common_dpne@v@.md) |
| Unleashed | `Common_dpnr@@` | none | 29/31/30 | [Details](Shaders/Unleashed/Common_dpnr@@.md) |
| Unleashed | `Common_dpr@@` | none | 27/29/28 | [Details](Shaders/Unleashed/Common_dpr@@.md) |
| Unleashed | `Common_dsn@@` | none | 28/30/29 | [Details](Shaders/Unleashed/Common_dsn@@.md) |
| Unleashed | `Common_dsne@@` | dual sphere / dual paraboloid (see sampling expressions) | 30/32/31 | [Details](Shaders/Unleashed/Common_dsne@@.md) |
| Unleashed | `DayEnemyMetalBody_dspne@@` | dual sphere / dual paraboloid (see sampling expressions) | 23/24/23 | [Details](Shaders/Unleashed/DayEnemyMetalBody_dspne@@.md) |
| Unleashed | `DayEnemyMetalSocket_dspne@@` | dual sphere / dual paraboloid (see sampling expressions) | 23/24/23 | [Details](Shaders/Unleashed/DayEnemyMetalSocket_dspne@@.md) |
| Unleashed | `DayEnemyMetal_dspne@@` | dual sphere / dual paraboloid (see sampling expressions) | 23/24/23 | [Details](Shaders/Unleashed/DayEnemyMetal_dspne@@.md) |
| Unleashed | `IgnoreLight_d@@` | none | 5/7/6 | [Details](Shaders/Unleashed/IgnoreLight_d@@.md) |
| Unleashed | `IgnoreLight_d@v@` | none | 5/7/6 | [Details](Shaders/Unleashed/IgnoreLight_d@v@.md) |
| Unleashed | `Luminescence_d@E@` | none | 20/21/20 | [Details](Shaders/Unleashed/Luminescence_d@E@.md) |
| Unleashed | `Luminescence_d@Ev@` | none | 20/21/20 | [Details](Shaders/Unleashed/Luminescence_d@Ev@.md) |
| Unleashed | `Luminescence_dE@@` | none | 21/22/21 | [Details](Shaders/Unleashed/Luminescence_dE@@.md) |
| Unleashed | `Luminescence_dE@v@` | none | 21/22/21 | [Details](Shaders/Unleashed/Luminescence_dE@v@.md) |
| Unleashed | `Luminescence_dnE@@` | none | 23/24/23 | [Details](Shaders/Unleashed/Luminescence_dnE@@.md) |
| Unleashed | `Luminescence_dnE@v@` | none | 23/24/23 | [Details](Shaders/Unleashed/Luminescence_dnE@v@.md) |
| Unleashed | `Luminescence_dpE@@` | none | 23/24/23 | [Details](Shaders/Unleashed/Luminescence_dpE@@.md) |
| Unleashed | `Luminescence_dpnE@@` | none | 25/26/25 | [Details](Shaders/Unleashed/Luminescence_dpnE@@.md) |
| Unleashed | `NightEnemyCommon_drE@@` | none | 30/31/30 | [Details](Shaders/Unleashed/NightEnemyCommon_drE@@.md) |
| Unleashed | `SonicCloth_ds@@` | none | 21/22/21 | [Details](Shaders/Unleashed/SonicCloth_ds@@.md) |
| Unleashed | `SonicCloth_dsn@@` | none | 23/24/23 | [Details](Shaders/Unleashed/SonicCloth_dsn@@.md) |
| Unleashed | `SonicEnamel_dspe@@` | dual sphere / dual paraboloid (see sampling expressions) | 23/25/24 | [Details](Shaders/Unleashed/SonicEnamel_dspe@@.md) |
| Unleashed | `SonicEnamel_dspe@c@` | 4x3 cubemap cross | 24/26/25 | [Details](Shaders/Unleashed/SonicEnamel_dspe@c@.md) |
| Unleashed | `SonicEnamel_dspne@@` | dual sphere / dual paraboloid (see sampling expressions) | 25/27/26 | [Details](Shaders/Unleashed/SonicEnamel_dspne@@.md) |
| Unleashed | `SonicEnamel_dspne@c@` | 4x3 cubemap cross | 26/28/27 | [Details](Shaders/Unleashed/SonicEnamel_dspne@c@.md) |
| Unleashed | `SonicEye_dpe@@` | dual sphere / dual paraboloid (see sampling expressions) | 25/27/26 | [Details](Shaders/Unleashed/SonicEye_dpe@@.md) |
| Unleashed | `SonicEye_dpe@c@` | 4x3 cubemap cross | 26/28/27 | [Details](Shaders/Unleashed/SonicEye_dpe@c@.md) |
| Unleashed | `SonicEye_dpne@@` | dual sphere / dual paraboloid (see sampling expressions) | 27/29/28 | [Details](Shaders/Unleashed/SonicEye_dpne@@.md) |
| Unleashed | `SonicEye_dpne@c@` | 4x3 cubemap cross | 28/30/29 | [Details](Shaders/Unleashed/SonicEye_dpne@c@.md) |
| Unleashed | `SonicMetal_dpe@@` | sphere / source 2D projection (see sampling expressions) | 23/25/24 | [Details](Shaders/Unleashed/SonicMetal_dpe@@.md) |
| Unleashed | `SonicMetal_dpe@c@` | 4x3 cubemap cross | 24/26/25 | [Details](Shaders/Unleashed/SonicMetal_dpe@c@.md) |
| Unleashed | `SonicMetal_dpne@@` | sphere / source 2D projection (see sampling expressions) | 25/27/26 | [Details](Shaders/Unleashed/SonicMetal_dpne@@.md) |
| Unleashed | `SonicMetal_dpne@c@` | 4x3 cubemap cross | 26/28/27 | [Details](Shaders/Unleashed/SonicMetal_dpne@c@.md) |
| Unleashed | `SonicSkin_dnf@@` | none | 25/27/26 | [Details](Shaders/Unleashed/SonicSkin_dnf@@.md) |
| Unleashed | `SonicSkin_dspf@@` | none | 25/27/26 | [Details](Shaders/Unleashed/SonicSkin_dspf@@.md) |
| Unleashed | `SonicSkin_dspnf@@` | none | 27/29/28 | [Details](Shaders/Unleashed/SonicSkin_dspnf@@.md) |

## Exact aliases

Use the canonical file below in the same render-state folder; no redundant implementation is generated.

| Original alias | Canonical shader |
|---|---|
| `Generations/EnmMetal_dse@@.shader` | `Generations/EnmMetal_dse1@@.shader` |
| `Generations/EnmMetal_dsfe@@.shader` | `Generations/EnmMetal_dsfe1@@.shader` |
| `Generations/EnmMetal_dsne@@.shader` | `Generations/EnmMetal_dsne1@@.shader` |
| `Generations/EnmMetal_dsnfe@@.shader` | `Generations/EnmMetal_dsnfe1@@.shader` |
