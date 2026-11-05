import 'package:flutter/material.dart';
import '../constants/app_colors.dart';
import '../constants/app_typography.dart';

class AppLogo extends StatelessWidget {
  final double iconSize;
  final double fontSize;
  final bool showText;
  final MainAxisSize mainAxisSize;

  const AppLogo({
    super.key,
    this.iconSize = 22,
    this.fontSize = 22,
    this.showText = true,
    this.mainAxisSize = MainAxisSize.min,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: mainAxisSize,
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        Container(
          width: iconSize + 16,
          height: iconSize + 16,
          decoration: BoxDecoration(
            color: AppColors.primary,
            borderRadius: BorderRadius.circular(12),
          ),
          alignment: Alignment.center,
          child: Icon(
            Icons.apartment_rounded,
            color: Colors.white,
            size: iconSize,
          ),
        ),
        if (showText) ...[
          const SizedBox(width: 10),
          RichText(
            text: TextSpan(
              style: AppTypography.displayMedium.copyWith(
                fontSize: fontSize,
                letterSpacing: -0.5,
              ),
              children: const [
                TextSpan(
                  text: 'Trọ',
                  style: TextStyle(color: AppColors.textPrimary),
                ),
                TextSpan(
                  text: 'Việt',
                  style: TextStyle(color: AppColors.primary),
                ),
              ],
            ),
          ),
        ],
      ],
    );
  }
}
