from pathlib import Path

p = Path(r"E:\Projects\WildBerriesAnalyzer\WildBerriesAnalyzer.Mobile\Modules\MyFilters\Views\MyFiltersPage.xaml")
c = p.read_text(encoding="utf-8")
repls = [
    ('BackgroundColor="#F3F4F6"', 'BackgroundColor="{AppThemeBinding Light={StaticResource Background}, Dark={StaticResource BackgroundDark}}"'),
    ('BackgroundColor="{StaticResource Background}"', 'BackgroundColor="{AppThemeBinding Light={StaticResource Background}, Dark={StaticResource BackgroundDark}}"'),
    ('TextColor="#111827"', 'TextColor="{AppThemeBinding Light={StaticResource TextPrimary}, Dark={StaticResource TextPrimaryDark}}"'),
    ('TextColor="#6B7280"', 'TextColor="{AppThemeBinding Light={StaticResource TextSecondary}, Dark={StaticResource TextSecondaryDark}}"'),
    ('TextColor="#9CA3AF"', 'TextColor="{AppThemeBinding Light={StaticResource TextMuted}, Dark={StaticResource TextMutedDark}}"'),
    ('PlaceholderColor="#9CA3AF"', 'PlaceholderColor="{AppThemeBinding Light={StaticResource TextMuted}, Dark={StaticResource TextMutedDark}}"'),
    ('Stroke="#E5E7EB"', 'Stroke="{AppThemeBinding Light={StaticResource Outline}, Dark={StaticResource OutlineDark}}"'),
    ('BackgroundColor="White"', 'BackgroundColor="{AppThemeBinding Light={StaticResource Surface}, Dark={StaticResource SurfaceDark}}"'),
    ('BackgroundColor="#F9FAFB"', 'BackgroundColor="{AppThemeBinding Light={StaticResource SurfaceMuted}, Dark={StaticResource SurfaceMutedDark}}"'),
    ('BackgroundColor="#E5E7EB"', 'BackgroundColor="{AppThemeBinding Light={StaticResource SurfaceMuted}, Dark={StaticResource SurfaceMutedDark}}"'),
    ('TextColor="#DC2626"', 'TextColor="{AppThemeBinding Light={StaticResource Error}, Dark={StaticResource ErrorDark}}"'),
    ('BackgroundColor="{StaticResource Primary}"', 'BackgroundColor="{AppThemeBinding Light={StaticResource Primary}, Dark={StaticResource PrimaryDarkTheme}}"'),
    ('TextColor="{StaticResource Primary}"', 'TextColor="{AppThemeBinding Light={StaticResource Primary}, Dark={StaticResource PrimaryDarkTheme}}"'),
    ('Color="{StaticResource Primary}"', 'Color="{AppThemeBinding Light={StaticResource Primary}, Dark={StaticResource PrimaryDarkTheme}}"'),
]
for a, b in repls:
    c = c.replace(a, b)
p.write_text(c, encoding="utf-8")
print("bindings", c.count("AppThemeBinding"))
