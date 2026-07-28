/** DayPilot "Slate & Titanium" dark palette — mirrors wwwroot/css/site.css. */
export const theme = {
  colors: {
    bg: '#0a0f1d',
    surface: '#131a2b',
    surface2: '#0f1525',
    border: '#232c40',
    text: '#e6eaf2',
    muted: '#93a0b6',
    primary: '#818cf8',
    brand1: '#6366F1',
    brand2: '#9333EA',
    accent: '#F59E0B',
    danger: '#f87171',
    success: '#22c55e',
  },
  radius: { md: 16, sm: 11 },
  spacing: (n: number) => n * 4,
} as const;

export type Theme = typeof theme;
