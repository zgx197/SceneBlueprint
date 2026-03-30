export interface TextMeasureOptions {
  fontSize?: number;
  fontWeight?: number | string;
  fontFamily?: string;
}

export interface TextMeasurer {
  measure(text: string, options?: TextMeasureOptions): number;
}

const DEFAULT_OPTIONS: Required<TextMeasureOptions> = {
  fontSize: 12,
  fontWeight: 400,
  fontFamily: '"Segoe UI", "Microsoft YaHei UI", sans-serif',
};

function createFont(options: TextMeasureOptions | undefined) {
  const resolved = {
    ...DEFAULT_OPTIONS,
    ...options,
  };

  return `${resolved.fontWeight} ${resolved.fontSize}px ${resolved.fontFamily}`;
}

function approximateWidth(text: string, options?: TextMeasureOptions) {
  const resolved = {
    ...DEFAULT_OPTIONS,
    ...options,
  };

  return text.length * resolved.fontSize * 0.56;
}

export function createCanvasTextMeasurer(): TextMeasurer {
  let context: CanvasRenderingContext2D | null = null;

  return {
    measure(text, options) {
      if (typeof document === "undefined") {
        return approximateWidth(text, options);
      }

      if (!context) {
        const canvas = document.createElement("canvas");
        context = canvas.getContext("2d");
      }

      if (!context) {
        return approximateWidth(text, options);
      }

      context.font = createFont(options);
      return context.measureText(text).width;
    },
  };
}

export function estimateWrappedLineCount(
  text: string | undefined,
  maxWidth: number,
  measurer: TextMeasurer,
  options?: TextMeasureOptions,
) {
  if (!text) {
    return 0;
  }

  if (maxWidth <= 0) {
    return 1;
  }

  return Math.max(1, Math.ceil(measurer.measure(text, options) / maxWidth));
}
