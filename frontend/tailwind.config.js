/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{vue,js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        omnom: {
          dark: '#070811',         // Deep midnight obsidian
          darker: '#04050a',       // Pure black obsidian
          card: '#0d0f22',         // Midnight indigo slate
          cardHover: '#13162f',
          cardActive: '#191c3d',
          border: 'rgba(129, 140, 248, 0.12)',
          borderHighlight: 'rgba(168, 85, 247, 0.28)',
          cream: '#f1f5f9',        // Clean cool white
          indigo: '#6366f1',       // Electric Indigo
          violet: '#a855f7',       // Luminous Ultraviolet
          purple: '#8b5cf6',       // Neon Violet
          rose: '#f43f5e',         // Protein Rose
          cyan: '#38bdf8',         // Carb Cyan
          amber: '#fbbf24',        // Fat Amber
          emerald: '#10b981',      // Success / Verified
          // Compatibility aliases
          volt: '#a855f7',
          yellow: '#a855f7',
          coral: '#f43f5e',
          matcha: '#10b981',
          strawberry: '#f43f5e',
          blueberry: '#38bdf8',
          caramel: '#fbbf24',
        }
      },
      fontFamily: {
        sans: ['"Space Grotesk"', 'system-ui', 'sans-serif'],
        display: ['"Space Grotesk"', 'system-ui', 'sans-serif'],
        fun: ['"Space Grotesk"', 'system-ui', 'sans-serif'],
        mono: ['"Space Mono"', 'ui-monospace', 'monospace'],
      },
      boxShadow: {
        'omnom': '0 8px 32px -4px rgba(0, 0, 0, 0.7), 0 0 0 1px rgba(129, 140, 248, 0.12)',
        'nom-violet': '0 4px 24px -2px rgba(168, 85, 247, 0.35)',
        'nom-indigo': '0 4px 24px -2px rgba(99, 102, 241, 0.35)',
        'nom-rose': '0 4px 20px -2px rgba(244, 63, 94, 0.25)',
        'nom-volt': '0 4px 24px -2px rgba(168, 85, 247, 0.35)',
        'nom-yellow': '0 4px 24px -2px rgba(168, 85, 247, 0.35)',
        'nom-matcha': '0 4px 20px -2px rgba(16, 185, 129, 0.25)',
        'nom-strawberry': '0 4px 20px -2px rgba(244, 63, 94, 0.25)',
      }
    },
  },
  plugins: [],
}
