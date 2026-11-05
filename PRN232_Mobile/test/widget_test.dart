import 'package:flutter_test/flutter_test.dart';
import 'package:prn232_mobile/core/widgets/app_logo.dart';
import 'package:prn232_mobile/main.dart';

void main() {
  testWidgets('TroVietApp smoke test with router', (WidgetTester tester) async {
    await tester.pumpWidget(const TroVietApp());
    expect(find.byType(TroVietApp), findsOneWidget);
    expect(find.byType(AppLogo), findsWidgets);

    // Advance time past the splash timer (1.6s) to settle navigation to login
    await tester.pumpAndSettle(const Duration(seconds: 2));
    expect(find.text('Chào mừng bạn trở lại'), findsOneWidget);
  });
}
